using System;
using System.Collections.Generic;   // List型利用
using System.IO;                    //StreamReader利用

namespace CulcLFHF
{
    class Program
    {
        // 瞳孔径データを含むcsvファイルのパス
        private static string FilePath = @"../../../Resorces/pupilOnly.csv";
        // csvデータを格納するリスト
        private static List<String[]> pupilDatas = new List<String[]>();
        // 線形補間後のデータを格納するリスト
        private static float[,] fixedDatas;
        

        public static void Main(string[] args)
        { 
            // ファイル読み込み準備
            FileStream fs = File.Open(FilePath, FileMode.Open, FileAccess.Read);
            // ファイル読み込み
            using (StreamReader sr = new StreamReader(fs))
            {
                while (!sr.EndOfStream) // 読み終わるまで続ける
                {
                    string line = sr.ReadLine();    // １行分の文字列をlineに格納
                    pupilDatas.Add(line.Split(','));    // カンマ区切りでリストに格納
                }
            }

            fixedDatas = new float[2, pupilDatas.Count];  // column名の1行目を除く行数
            fixedDatas[0, 0] = fixedDatas[0, 1] = -1;
            // 補完必要数
            int Ncount = 0;
            // 補完開始位置
            int lstart;

            // 左右瞳孔径を順に見ていく
            for(int LR = 0; LR < 2; LR++)
            {
                // 各データを順に見ていく(1行目はColumn名なので飛ばしてi=1から)
                for(int i = 1; i < pupilDatas.Count; i++)
                {
                    if (pupilDatas[i][LR].Equals(""))   // 空値を見つけた場合
                    {
                        // 最初の空値のとき，そこが線形補間開始位置となるので記憶
                        if (Ncount == 0) lstart = i;
                        // 空白の個数をカウント
                        Ncount++;
                    }
                    else
                    {
                        // 値をそのまま代入(string→float変換だけする)
                        fixedDatas[LR, i] = float.Parse(pupilDatas[i][LR]);

                        // 空値を見つけた後の正常値ということは，ここまでの線形補完が必要
                        if (Ncount > 0) Console.WriteLine(Ncount.ToString() + "data should be fixed!");

                        // 空値の個数をリセット
                        Ncount = 0;
                    }

                    Console.WriteLine(Ncount);
                }
            }

            

        }

        // 線形補完メソッド，デュエルスタンバイ！
        private void lerp(int start)
        {

        }
    }
}
