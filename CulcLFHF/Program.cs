using System;
using System.Collections.Generic;   // List型利用
using System.IO;                    // StreamReader利用
using System.Numerics;                          // Complex型利用
using MathNet.Numerics.IntegralTransforms;  // FFT用ライブラリ

namespace CulcLFHF
{
	class Program
	{
		// 瞳孔径データを含むcsvファイルのパス
		private static string FilePath = @"../../../Resorces/pupilData.csv";
		// csvデータを格納するリスト
		private static List<String[]> pupilDatas = new List<String[]>();
		// 線形補間後のデータを格納するリスト
		private static float[,] fixedDatas;
		private static int dataNum;
		private static int initTime;


		public static void Main(string[] args)
		{
			// CSV読み込み，読み込んだデータをpupilDatasのリストに格納
			CSVReader(FilePath, pupilDatas);
			// データの行数を取得
			dataNum = pupilDatas.Count;
			// 最初のタイムスタンプを取得
			initTime = int.Parse(pupilDatas[1][0]);

			// 線形補間
			InitData();

			// フーリエ変換(LF/HF算出)
			FFT();
		}

		private static void InitData()
        {
			// 線形補間後のデータを格納する２次元配列
			fixedDatas = new float[3, dataNum]; //timestamp,left,rightの3列

			// 補完必要数
			int Ncount = 0;
			// 補完開始位置
			int lstart = -1;

			// 左右瞳孔径を順に見ていく
			for (int LR = 1; LR < 3; LR++)	//1列目はtimestamp
			{
				// 各データを順に見ていく(1行目はColumn名なので飛ばしてi=1から)
				for (int i = 1; i < dataNum; i++)
				{
					fixedDatas[0, i] = int.Parse(pupilDatas[i][0]);     //timestamp

					if (pupilDatas[i][LR].Equals("-1"))   // -1(欠損値)を見つけた場合
					{
						// 最初の空値のとき，そこが線形補間開始位置となるので記憶
						if (Ncount == 0)
                        {
							lstart = i;
							Console.WriteLine(lstart + "行目から線形補完 要求");
						}
						// 空白の個数をカウント
						Ncount++;
					}
					else
					{
						// 値をそのまま代入(string→float変換だけする)
						fixedDatas[LR, i] = float.Parse(pupilDatas[i][LR]);	//瞳孔径

						// 空値を見つけた後の正常値ということは，ここまでの線形補完が必要
						if (Ncount > 0) Lerp(LR, lstart, i, Ncount);

						// 空値の個数をリセット
						Ncount = 0;
					}
				}
			}
			// 線形補間後のデータを書き出し(デバッグも兼ねて)
			CSVWriter("_lerp", fixedDatas, dataNum);
		}

		// 線形補完メソッド
		private static void Lerp(int LR, int lstart, int lend, int count)
		{
			// 欠損値を順次補完していく
			for (int i = 1, index = lstart; i <= count; i++, index++)
			{
				// 補完中の位置を比で割り出す
				float t = (float)i / (count + 1);
				// 内分点計算により値を補完
				fixedDatas[LR, index] = (1 - t) * fixedDatas[LR, lstart - 1] + t * fixedDatas[LR, lend];
			}
			Console.WriteLine(lstart + "行目から" + (lend-1) + "行目の線形補間 完了");
		}


		private static void CSVReader(String FilePath, List<String[]> datas)
		{
			// ファイル読み込み準備
			FileStream fs = File.Open(FilePath, FileMode.Open, FileAccess.Read);
			// ファイル読み込み
			using (StreamReader sr = new StreamReader(fs))
			{
				while (!sr.EndOfStream) // 読み終わるまで続ける
				{
					string line = sr.ReadLine();    // １行分の文字列をlineに格納
					datas.Add(line.Split(','));    // カンマ区切りでリストに格納
				}
			}
		}

		private static void CSVWriter(String addName, float[,] data, int length)
		{
			// ファイル名に"_lerp"をつけたファイルを作成するためのパス名
			String writefilepath = FilePath.Replace(".csv", addName + ".csv");
			using (StreamWriter sw = new StreamWriter(writefilepath, false) { AutoFlush = true })
			{
				sw.WriteLine("timestamp, realtime, Left, Right");
				for (int i = 1; i < length; i++)
				{
					Console.WriteLine(i+"行目書き出し");
					sw.WriteLine(data[0, i] + ", " + (data[0, i]-initTime) + ", " + data[1, i] + ", " + data[2, i]);
				}
			}
		}

		// フーリエ変換(LF/HF算出)メソッド
		private static void FFT()
		{
			if (fixedDatas.GetLength(1) < 4097)	//データ数(csvの行数)の確認
			{
				Console.WriteLine("データ数が4096以上必要です");
				return;
			}

			// フーリエ変換の回数
			int t = (fixedDatas.GetLength(1) - 4097) / 128;
			// フーリエ変換後の値の配列
			float[,] LF_HF = new float[2,t];    // LF/HF の結果を格納
			float[,] HF_LFHF = new float[2,t];	// HF/(LF+HF) の結果を格納

			// 左右を順に
			for (int LR = 0; LR < 2; LR++)
			{
				// 4096点で128点間隔でフーリエ変換していく
				for (int i = 1, cut = 0; cut < t; i += 128, cut++)
				{
					// cutフレーム目の4096個のデータを代入
					float[] fset = new float[4096];
					for (int d = 0; d < 4096; d++) fset[d] = (float)fixedDatas[LR, i + d];
					//fset配列の値をWindow処理する。今回はハニング窓を使用
					fset = Windowing(fset, WindowFunc.Hanning);

					//Complex型に格納(複素数型だが実部がfset[c],虚部が０)
					Complex[] x = new Complex[4096];    // フーリエ変換後の値(複素数なのでComplex型)
					for (int c = 0; c < 4096; c++) x[c] = new Complex(fset[c], 0);

					// FFT処理の実行
					Fourier.Forward(x, FourierOptions.Matlab);
					Console.WriteLine();

					// FFT処理後のデータを格納
					float[] dataFFT = new float[2048];
					for (int c = 0; c < 2048; c++) dataFFT[c] = (float)(x[c].Magnitude / 4096 * 2); //絶対値を正規化

					float LF = dataFFT[2] + dataFFT[3] + dataFFT[4] + dataFFT[5] + dataFFT[6];
					float HF = dataFFT[7] + dataFFT[8] + dataFFT[9] + dataFFT[10] + dataFFT[11] + dataFFT[12] + dataFFT[13] + dataFFT[14] + dataFFT[15] + dataFFT[16] + dataFFT[17] + dataFFT[18];

					LF_HF[LR, cut] = LF / HF;
					HF_LFHF[LR, cut] = HF / (LF + HF);
				}
			}
			// LF/HFのデータを書き出し
			CSVWriter("_lfhf", LF_HF, t);
			CSVWriter("_hf-lfhf", HF_LFHF, t);
		}

		// フーリエ変換の窓関数
		public enum WindowFunc
		{
			Hamming, Hanning, Blackman, Rectangular
		}

		public static float[] Windowing(float[] data, WindowFunc windowfunc)
		{
			int size = data.Length;
			float[] windata = new float[size];

			for (int i = 0; i < size; i++)
			{
				double winValue = 0;
				// 各々の窓関数
				switch (windowfunc)
				{
					case WindowFunc.Hamming:
						winValue = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (size - 1));
						break;

					case WindowFunc.Hanning:
						winValue = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (size - 1));
						break;

					case WindowFunc.Blackman:
						winValue = 0.42 - 0.5 * Math.Cos(2 * Math.PI * i / (size - 1) + 0.08 * Math.Cos(4 * Math.PI * i / (size - 1)));
						break;

					case WindowFunc.Rectangular:
						winValue = 1.0;
						break;

					default:
						winValue = 1.0;
						break;
				}
				// 窓関数を掛け算
				windata[i] = data[i] * (float)winValue;
			}
			return windata;
		}
	}
}
