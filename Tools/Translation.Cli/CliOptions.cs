using System.Collections.Generic;

namespace Translation.Cli
{
    internal sealed class CliOptions
    {
        public string Command { get; private set; } = "help";

        public string ConfigPath { get; private set; }

        public string OutputPath { get; private set; }

        public string Version { get; private set; }

        public bool Strict { get; private set; }

        public bool Verbose { get; private set; }

        public bool Force { get; private set; }

        /// <summary> sync: 게임 DB 에서 사라진 키의 행을 지운다 </summary>
        public bool Prune { get; private set; }

        /// <summary> sync: 결과만 보여 주고 시트에 쓰지 않는다 </summary>
        public bool DryRun { get; private set; }

        public List<string> Positional { get; } = new List<string>();

        public static CliOptions Parse(string[] args, out string error)
        {
            error = null;
            var options = new CliOptions();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                switch (arg)
                {
                    case "-c":
                    case "--config":
                        if (!TryTake(args, ref i, out var config))
                        {
                            error = arg + "에 값이 필요합니다.";
                            return options;
                        }

                        options.ConfigPath = config;
                        continue;

                    case "-o":
                    case "--out":
                        if (!TryTake(args, ref i, out var output))
                        {
                            error = arg + "에 값이 필요합니다.";
                            return options;
                        }

                        options.OutputPath = output;
                        continue;

                    case "-v":
                    case "--version":
                        if (!TryTake(args, ref i, out var version))
                        {
                            error = arg + "에 값이 필요합니다.";
                            return options;
                        }

                        options.Version = version;
                        continue;

                    case "--strict":
                        options.Strict = true;
                        continue;

                    case "--verbose":
                        options.Verbose = true;
                        continue;

                    case "--force":
                        options.Force = true;
                        continue;

                    case "--prune":
                        options.Prune = true;
                        continue;

                    case "--dry-run":
                        options.DryRun = true;
                        continue;

                    case "-h":
                    case "--help":
                        options.Command = "help";
                        return options;
                }

                if (arg.StartsWith("-"))
                {
                    error = "알 수 없는 옵션: " + arg;
                    return options;
                }

                if (options.Command == "help" && options.Positional.Count == 0)
                {
                    options.Command = arg;
                    continue;
                }

                options.Positional.Add(arg);
            }

            return options;
        }

        private static bool TryTake(string[] args, ref int index, out string value)
        {
            if (index + 1 >= args.Length)
            {
                value = null;
                return false;
            }

            value = args[++index];
            return true;
        }
    }
}
