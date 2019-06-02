using System;
namespace BulkRename
{
    public class BulkRenameParameter
    {
        [CommandLine.Option("dir", Default =".", HelpText = "file location")]
        public string SourceDir { get; set; }

        [CommandLine.Option("pattern", Required = true, HelpText = "search pattern of target files")]
        public string FilePattern { get; set; }

        [CommandLine.Option("sortnum", Default = false, HelpText = "Sort original file by numeric value in the file name")]
        public bool SortByNumber { get; set; }

        [CommandLine.Option("prefix", HelpText = "Prefix of renamed file")]
        public string Prefix { get; set; }

        [CommandLine.Option("origin", Default = false, HelpText = "Determine if original file name should be included in the renamed file")]
        public bool UseOriginalName { get; set; }

        [CommandLine.Option("suffix", Default = false, HelpText = "Determine if sequencial number is add at the end of renamed file")]
        public bool AddSequence { get; set; }
    }
}
