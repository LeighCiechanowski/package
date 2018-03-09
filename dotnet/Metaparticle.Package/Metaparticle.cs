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
    public class Driver
    {
        private Config config;
        private RuntimeConfig runtimeConfig;

        public Driver(Config config, RuntimeConfig runtimeConfig)
        {
            this.config = config;
            this.runtimeConfig = runtimeConfig;
        }

        public static bool InDockerContainer()
        {
            switch (System.Environment.GetEnvironmentVariable("METAPARTICLE_IN_CONTAINER"))
            {
                case "true":
                case "1":
                    return true;
                case "false":
                case "0":
                    return false;
            }
            // This only works on Linux
            const string cgroupPath = "/proc/1/cgroup";
            if (File.Exists(cgroupPath)) {
                var info = File.ReadAllText(cgroupPath);
                // This is a little approximate...
                return info.IndexOf("docker") != -1 || info.IndexOf("kubepods") != -1;
            }
            return false;
        }
        public static void Containerize(string[] args, Action main, IDockerFileFactory dockerFileBuilder = null)
        {
            if (InDockerContainer())
            {
                main();
                return;
            }
            Config config = new Config();
            RuntimeConfig runtimeConfig = null;
            var trace = new StackTrace();
            foreach (object attribute in trace.GetFrame(1).GetMethod().GetCustomAttributes(true))
            {
                if (attribute is Config)
                {
                    config = (Config) attribute;
                }
                if (attribute is RuntimeConfig)
                {
                    runtimeConfig = (RuntimeConfig) attribute;
                }
            }
            var mp = new Driver(config, runtimeConfig);
            dockerFileBuilder.Build(args, config, runtimeConfig);
        }
    }
}
