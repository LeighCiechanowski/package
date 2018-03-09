using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using dockerfile;
using static Metaparticle.Package.Util;
using RuntimeConfig = Metaparticle.Runtime.Config;

namespace Metaparticle.Package
{
    public class DockerFileFactory : IDockerFileFactory
    {
        private Config config;
        private RuntimeConfig runtimeConfig;
        public void Build(string[] args, Config config, RuntimeConfig runtimeConfig)
            {
                this.config = config;
                this.runtimeConfig = runtimeConfig;
                var proc = Process.GetCurrentProcess();
                var procName = proc.ProcessName;
                string exe = null;
                string dir = null;
                TextWriter o = config.Verbose ? Console.Out : null;
                TextWriter e = config.Quiet ? Console.Error : null;
                if (procName == "dotnet")
                {
                    dir = "bin/release/netcoreapp2.0/debian.8-x64/publish";
                    Exec("dotnet", "publish -r debian.8-x64 -c release", stdout: o, stderr: e);
                    //var dirInfo = new UnixDirectoryInfo(dir);
                    var files = Directory.GetFiles(dir);
                    foreach (var filePath in files)
                    {
                        var file = new FileInfo(filePath);
                        if (file.Name.EndsWith(".runtimeconfig.json"))
                        {
                            exe = file.Name.Substring(0, file.Name.Length - ".runtimeconfig.json".Length);
                        }
                    }
                }
                else
                {
                    exe = procName;
                    var prog = proc.MainModule.FileName;
                    dir = Directory.GetParent(prog).FullName;
                }
                var dockerfilename = writeDockerfile(dir, exe, args, config);
                var builder = getBuilder();

                string imgName = (string.IsNullOrEmpty(config.Repository) ? exe : config.Repository);
                if (!string.IsNullOrEmpty(config.Version)) {
                    imgName += ":" + config.Version;
                }
                if (!builder.Build(dockerfilename, imgName, stdout: o, stderr: e))
                {
                    Console.Error.WriteLine("Image build failed.");
                    return;
                }

                if (config.Publish)
                {
                    if (!builder.Push(imgName, stdout: o, stderr: e))
                    {
                        Console.Error.WriteLine("Image push failed.");
                        return;
                    }
                }

                if (runtimeConfig == null)
                {
                    return;
                }

                var exec = getExecutor();
                if (exec.PublishRequired() && !config.Publish)
                {
                    Console.Error.WriteLine("Image publish is required, but image was not published. Set publish to true in the package config.");
                    return;
                }
                var id = exec.Run(imgName, runtimeConfig);

                Console.CancelKeyPress += delegate
                {
                    exec.Cancel(id);
                };

                exec.Logs(id, Console.Out, Console.Error);
            }

            private string writeDockerfile(string dir, string exe, string[] args, Config config)
            {
                var dockerfilename = dir + "/Dockerfile";
                if (!string.IsNullOrEmpty(config.Dockerfile)) {
                    File.Copy(config.Dockerfile, dockerfilename);
                    return dockerfilename;
                }
                var instructions = new List<Instruction>();
                instructions.Add(new Instruction("FROM", "debian:9"));
                instructions.Add(new Instruction("RUN", " apt-get update && apt-get -qq -y install libunwind8 libicu57 libssl1.0 liblttng-ust0 libcurl3 libuuid1 libkrb5-3 zlib1g"));
                // TODO: lots of things here are constant, figure out how to cache for perf?
                instructions.Add(new Instruction("COPY", string.Format("* /exe/", dir)));
                instructions.Add(new Instruction("CMD", string.Format("/exe/{0} {1}", exe, getArgs(args))));

                var df = new Dockerfile(instructions.ToArray(), new Comment[0]);
                File.WriteAllText(dockerfilename, df.Contents());

                return dockerfilename;
            }
            
        private ImageBuilder getBuilder()
        {
            switch (config.Builder.ToLowerInvariant())
            {
                case "docker":
                    return new DockerBuilder();
                case "aci":
                    return new DockerBuilder();
                default:
                    return null;
            }
        }

        private ContainerExecutor getExecutor()
        {
            if (runtimeConfig == null) {
                return null;
            }
            switch (runtimeConfig.Executor.ToLowerInvariant())
            {
                case "docker":
                    return new DockerExecutor();
                case "aci":
                    return new AciExecutor(runtimeConfig);
                case "metaparticle":
                    return new MetaparticleExecutor();
                default:
                    return null;
            }
        }
        private static string getArgs(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "";
            }
            var b = new StringBuilder();
            foreach (var arg in args)
            {
                b.Append(arg);
                b.Append(" ");
            }
            return b.ToString().Trim();
        }
    }
}