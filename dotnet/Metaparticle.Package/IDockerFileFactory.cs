using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using dockerfile;
using static Metaparticle.Package.Util;
using RuntimeConfig = Metaparticle.Runtime.Config;

namespace Metaparticle.Package
{
    public interface IDockerFileFactory
    {
        void Build(string[] args, Config config, RuntimeConfig runtimeConfig);
    }
}