using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using CommandLine;
using System.Text;

namespace BulkRename
{
    class Program
    {
        static void Main(string[] args)
        {
            // Perse parameter
            var result = Parser.Default.ParseArguments<BulkRenameParameter>(args);
            result.WithParsed<BulkRenameParameter>(RunMain);
        }

        /// <summary>
        /// メインの処理を実行します。コマンドラインパラメータに基づいてファイルの一括改名を行います。
        /// </summary>
        /// <param name="opt">コマンドラインから解析されたパラメータ</param>
        /// <exception cref="Exception">ファイル操作中に発生した例外</exception>
        private static void RunMain(BulkRenameParameter opt)
        {
            try
            {
                // list files
                var originalFiles = GetFiles(opt.SourceDir, opt.FilePattern);
                if (originalFiles.Length == 0)
                {
                    Console.WriteLine("対象ファイルが見つかりませんでした。");
                    return;
                }

                // sort files
                var sortedFiles = SortFiles(originalFiles, opt.SortByNumber);

                // assign new name
                var renameFiles = AssignFileName(sortedFiles, opt.Prefix, opt.UseOriginalName, opt.AddSequence);

                // 事前検証
                ValidateRenameOperation(sortedFiles, renameFiles);

                // review new file name
                ReviewRename(sortedFiles, renameFiles);

                Console.WriteLine("続行するには任意のキーを押してください。中止する場合は Ctrl+C を押してください。");
                Console.ReadKey();

                // rename
                BulkRename(sortedFiles, renameFiles);
                
                Console.WriteLine("ファイルの改名が完了しました。");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"エラーが発生しました: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// ファイル改名操作の事前検証を行います。
        /// </summary>
        /// <param name="oldNames">元のファイル名の配列</param>
        /// <param name="newNames">新しいファイル名の配列</param>
        /// <exception cref="InvalidOperationException">ファイル名の数が一致しない場合、または新しいファイル名に重複がある場合</exception>
        /// <exception cref="IOException">新しいファイル名が既存のファイルと衝突する場合</exception>
        private static void ValidateRenameOperation(string[] oldNames, string[] newNames)
        {
            if (oldNames.Length != newNames.Length)
            {
                throw new InvalidOperationException("ファイル名の数が一致しません。");
            }

            // 新しいファイル名の重複チェック
            var duplicates = newNames.GroupBy(x => x)
                                   .Where(g => g.Count() > 1)
                                   .Select(g => g.Key);
            if (duplicates.Any())
            {
                throw new InvalidOperationException($"新しいファイル名に重複があります: {string.Join(", ", duplicates)}");
            }

            // 既存ファイルとの衝突チェック
            for (var i = 0; i < oldNames.Length; i++)
            {
                string sourceDir = Path.GetDirectoryName(oldNames[i]) ?? string.Empty;
                string newPath = Path.Combine(sourceDir, newNames[i]);

                if (File.Exists(newPath) && !string.Equals(oldNames[i], newPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"ファイル '{newPath}' は既に存在します。");
                }
            }
        }

        /// <summary>
        /// 指定されたディレクトリから検索パターンに一致するファイルを取得します。
        /// </summary>
        /// <param name="path">検索対象のディレクトリパス</param>
        /// <param name="searchPattern">検索パターン（ワイルドカード使用可）</param>
        /// <returns>検索パターンに一致するファイルパスの配列</returns>
        /// <exception cref="DirectoryNotFoundException">指定されたディレクトリが存在しない場合</exception>
        private static string[] GetFiles(string path, string searchPattern)
        {
            return Directory.GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// ファイル名の配列を指定された方法で並び替えます。
        /// </summary>
        /// <param name="files">並び替えるファイル名の配列</param>
        /// <param name="sortByNumber">数値でソートする場合はtrue、アルファベット順の場合はfalse</param>
        /// <returns>並び替えられたファイル名の配列</returns>
        private static string[] SortFiles(string[] files, bool sortByNumber)
        {
            var sorted = new List<string>();
            if (sortByNumber)
            {
                return SortFilesByNumber(files);
            }
            else
            {
                sorted.AddRange(files);
                sorted.Sort();
            }
            return sorted.ToArray();
        }

        /// <summary>
        /// ファイル名に含まれる数値でファイルを並び替えます。
        /// </summary>
        /// <param name="files">並び替えるファイル名の配列</param>
        /// <returns>数値順に並び替えられたファイル名の配列</returns>
        private static string[] SortFilesByNumber(string[] files)
        {
            var matchNumber = new Regex("[0-9]+");

            var tuples = new List<Tuple<int, string>>();
            foreach (var filename in files)
            {
                string filenumber = Path.GetFileNameWithoutExtension(filename);
                int index = Int32.MaxValue;

                if (matchNumber.IsMatch(filenumber))
                {
                    var matches = matchNumber.Matches(filenumber);
                    if (matches.Count > 0)
                        index = Convert.ToInt32(matches[matches.Count - 1].Value);
                }

                tuples.Add(new Tuple<int, string>(index, filename));
            }

            var sortByNumeric = from item in tuples orderby item.Item1, item.Item2 select item.Item2;
            return sortByNumeric.ToArray();
        }

        /// <summary>
        /// 新しいファイル名を生成します。
        /// </summary>
        /// <param name="files">元のファイル名の配列</param>
        /// <param name="prefix">新しいファイル名の接頭辞</param>
        /// <param name="useOrigin">元のファイル名を使用するかどうか</param>
        /// <param name="useSequence">連番を付与するかどうか</param>
        /// <returns>生成された新しいファイル名の配列</returns>
        private static string[] AssignFileName(string[] files, string prefix, bool useOrigin, bool useSequence)
        {
            var count = files.Length;
            var sequenceFormat = "{0:D" + (count.ToString().Length).ToString() + "}";

            var newFiles = new List<string>();

            var index = 0;
            foreach (var oldfile in files)
            {
                index++;

                StringBuilder newname = new StringBuilder();

                if (!string.IsNullOrEmpty(prefix))
                    newname.Append(prefix);

                if(useOrigin)
                    newname.Append(Path.GetFileNameWithoutExtension(oldfile));

                if(useSequence)
                    newname.AppendFormat(sequenceFormat, index);

                if (newname.Length > 0)
                    newname.Append(Path.GetExtension(oldfile));
                else
                    newname.Append(Path.GetFileName(oldfile));

                newFiles.Add(newname.ToString());
            }

            return newFiles.ToArray();
        }

        /// <summary>
        /// 改名前後のファイル名を表示し、ユーザーに確認を求めます。
        /// </summary>
        /// <param name="oldname">元のファイル名の配列</param>
        /// <param name="newname">新しいファイル名の配列</param>
        /// <exception cref="InvalidOperationException">ファイル名の配列の長さが一致しない場合</exception>
        private static void ReviewRename(string[] oldname, string[] newname)
        {
            if (oldname.Length != newname.Length)
                throw new InvalidOperationException();

            for (var ii = 0; ii < oldname.Length; ii++)
            {
                Console.WriteLine($"rename [{oldname[ii]}] -> [{newname[ii]}]");
            }
        }

        /// <summary>
        /// ファイルの一括改名を実行します。
        /// </summary>
        /// <param name="oldNames">元のファイル名の配列</param>
        /// <param name="newNames">新しいファイル名の配列</param>
        /// <exception cref="IOException">ファイルの改名中にエラーが発生した場合</exception>
        private static void BulkRename(string[] oldNames, string[] newNames)
        {
            var operations = new List<(string oldPath, string newPath)>();

            // 改名操作のリストを作成
            for (var i = 0; i < oldNames.Length; i++)
            {
                string sourceDir = Path.GetDirectoryName(oldNames[i]) ?? string.Empty;
                string newPath = Path.Combine(sourceDir, newNames[i]);
                operations.Add((oldNames[i], newPath));
            }

            // 一時的な名前を使用して改名を実行
            try
            {
                foreach (var (oldPath, newPath) in operations)
                {
                    string tempPath = Path.Combine(
                        Path.GetDirectoryName(oldPath) ?? string.Empty,
                        Guid.NewGuid().ToString() + Path.GetExtension(oldPath)
                    );

                    File.Move(oldPath, tempPath);
                    File.Move(tempPath, newPath);

                    Console.WriteLine($"改名完了: {Path.GetFileName(oldPath)} -> {Path.GetFileName(newPath)}");
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"ファイルの改名中にエラーが発生しました: {ex.Message}");
            }
        }
    }
}
