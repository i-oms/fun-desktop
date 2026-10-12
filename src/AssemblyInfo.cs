using System.Reflection;

// Version / publisher metadata shown in Explorer properties and used by
// SmartScreen / Defender reputation. The trusted publisher name itself comes
// from the code-signing certificate (see build.cmd), not from these strings,
// but a blank exe looks far more suspicious to heuristics.
// C# 5 compatible on purpose (built-in csc.exe).
[assembly: AssemblyTitle("IdleWalker")]
[assembly: AssemblyDescription("Tiny Windows desktop companion - Minions, Mr. Bean and friends.")]
[assembly: AssemblyProduct("IdleWalker")]
[assembly: AssemblyCompany("IdleWalker")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
