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

        private static void RunMain(BulkRenameParameter opt)
        {
            // list files
            var originalFiles = GetFiles(opt.SourceDir, opt.FilePattern);

            // sort files
            var sortedFiles = SortFiles(originalFiles, opt.SortByNumber);

            // assign new name
            var renameFiles = AssignFileName(sortedFiles, opt.Prefix, opt.UseOriginalName, opt.AddSequence);

            // review new file name
            ReviewRename(sortedFiles, renameFiles);

            Console.WriteLine("Press any key to proceed");
            Console.ReadKey();

            // rename
            BulkRename(sortedFiles, renameFiles);
        }

        private static string[] GetFiles(string path, string searchPattern)
        {
            return Directory.GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

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

        private static void ReviewRename(string[] oldname, string[] newname)
        {
            if (oldname.Length != newname.Length)
                throw new InvalidOperationException();

            for (var ii = 0; ii < oldname.Length; ii++)
            {
                Console.WriteLine($"rename [{oldname[ii]}] -> [{newname[ii]}]");
            }
        }

        private static void BulkRename(string[] oldname, string[] newname)
        {
            for (var ii = 0; ii < oldname.Length; ii++)
            {
                string dir = Path.GetDirectoryName(oldname[ii]);
                File.Move(oldname[ii], dir + Path.DirectorySeparatorChar + newname[ii]);
            }
        }
    }
}
