using System.Runtime.CompilerServices;
using Throne.Api.Cli;

namespace Throne.Api.Tests.Infrastructure;

internal static class TestHostBootstrap
{
    /// <summary>
    /// Integration tests boot <c>Program</c> through <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>,
    /// which starts the entry point with no CLI verb — that resolves to <c>start</c>, whose
    /// single-instance gate records this (shared) test process's pid as a live daemon and then
    /// makes every later in-process boot return without building a host («The entry point exited
    /// without ever building an IHost»). Routing those boots to the raw <c>serve</c> host removes
    /// the pid gate entirely. Set once at assembly load — a constant value, so no env-var race
    /// with the parallel host boots.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize() =>
        Environment.SetEnvironmentVariable(ThroneCli.CommandEnvVar, "serve");
}
