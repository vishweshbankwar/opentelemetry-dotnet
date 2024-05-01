// <auto-generated /> (Turns off StyleCop analysis in this file.)
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*
 Note: This class was copied from
 https://github.com/dotnet/runtime/blob/e13e7388dedc6672381e61592c2e74385fe781a5/src/libraries/Microsoft.Extensions.Options/src/OptionsFactory.cs
 and then modified to have delegate features needed by the SDK. In the future if
 we take a dependency on Microsoft.Extensions.Options v5.0.0 (or greater), much
 of this can be removed in favor of the "CreateInstance" API added in 5:
 https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.optionsfactory-1.createinstance?view=dotnet-plat-ext-5.0.
 See https://github.com/open-telemetry/opentelemetry-dotnet/pull/4093 for an
 example of how that works.
*/

#nullable enable

using System.Diagnostics;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Options;

/// <summary>
/// Implementation of <see cref="IOptionsFactory{TOptions}"/>.
/// </summary>
/// <typeparam name="TOptions">The type of options being requested.</typeparam>
#if NET6_0_OR_GREATER
internal sealed class DelegatingOptionsFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions> :
#else
internal sealed class DelegatingOptionsFactory<TOptions> :
#endif
    IOptionsFactory<TOptions>
    where TOptions : class
{
    private readonly Func<IConfiguration, string, TOptions> optionsFactoryFunc;
    private readonly IConfiguration configuration;
    private readonly IConfigureOptions<TOptions>[] _setups;
    private readonly IPostConfigureOptions<TOptions>[] _postConfigures;
    private readonly IValidateOptions<TOptions>[] _validations;

    /// <summary>
    /// Initializes a new instance with the specified options configurations.
    /// </summary>
    /// <param name="optionsFactoryFunc">Factory delegate used to create <typeparamref name="TOptions"/> instances.</param>
    /// <param name="configuration"><see cref="IConfiguration"/>.</param>
    /// <param name="setups">The configuration actions to run.</param>
    /// <param name="postConfigures">The initialization actions to run.</param>
    /// <param name="validations">The validations to run.</param>
    public DelegatingOptionsFactory(
        Func<IConfiguration, string, TOptions> optionsFactoryFunc,
        IConfiguration configuration,
        IEnumerable<IConfigureOptions<TOptions>> setups,
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures,
        IEnumerable<IValidateOptions<TOptions>> validations)
    {
        // The default DI container uses arrays under the covers. Take advantage of this knowledge
        // by checking for an array and enumerate over that, so we don't need to allocate an enumerator.
        // When it isn't already an array, convert it to one, but don't use System.Linq to avoid pulling Linq in to
        // small trimmed applications.

        Debug.Assert(optionsFactoryFunc != null, "optionsFactoryFunc was null");
        Debug.Assert(configuration != null, "configuration was null");

        this.optionsFactoryFunc = optionsFactoryFunc!;
        this.configuration = configuration!;
        _setups = setups as IConfigureOptions<TOptions>[] ?? new List<IConfigureOptions<TOptions>>(setups).ToArray();
        _postConfigures = postConfigures as IPostConfigureOptions<TOptions>[] ?? new List<IPostConfigureOptions<TOptions>>(postConfigures).ToArray();
        _validations = validations as IValidateOptions<TOptions>[] ?? new List<IValidateOptions<TOptions>>(validations).ToArray();
    }

    /// <summary>
    /// Returns a configured <typeparamref name="TOptions"/> instance with the given <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the <typeparamref name="TOptions"/> instance to create.</param>
    /// <returns>The created <typeparamref name="TOptions"/> instance with the given <paramref name="name"/>.</returns>
    /// <exception cref="OptionsValidationException">One or more <see cref="IValidateOptions{TOptions}"/> return failed <see cref="ValidateOptionsResult"/> when validating the <typeparamref name="TOptions"/> instance been created.</exception>
    /// <exception cref="MissingMethodException">The <typeparamref name="TOptions"/> does not have a public parameterless constructor or <typeparamref name="TOptions"/> is <see langword="abstract"/>.</exception>
    public TOptions Create(string name)
    {
        TOptions options = this.optionsFactoryFunc(this.configuration, name);

        foreach (IConfigureOptions<TOptions> setup in _setups)
        {
            if (setup is IConfigureNamedOptions<TOptions> namedSetup)
            {
                namedSetup.Configure(name, options);
            }
            else if (name == Options.DefaultName)
            {
                setup.Configure(options);
            }
        }
        foreach (IPostConfigureOptions<TOptions> post in _postConfigures)
        {
            post.PostConfigure(name, options);
        }

        if (_validations.Length > 0)
        {
            var failures = new List<string>();
            foreach (IValidateOptions<TOptions> validate in _validations)
            {
                ValidateOptionsResult result = validate.Validate(name, options);
                if (result is not null && result.Failed)
                {
                    failures.AddRange(result.Failures);
                }
            }
            if (failures.Count > 0)
            {
                throw new OptionsValidationException(name, typeof(TOptions), failures);
            }
        }

        return options;
    }
}
