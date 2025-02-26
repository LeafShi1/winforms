﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Serialization;

namespace System.Private.Windows.BinaryFormat;

internal sealed class DefaultTypeResolver : ITypeResolver
{
    private readonly bool _simpleAssemblyMatching;
    private readonly ITypeResolver? _binder;

    private readonly Dictionary<string, Assembly> _assemblies = [];
    private readonly Dictionary<string, Type> _types = [];

    internal DefaultTypeResolver(DeserializationOptions options)
    {
        _simpleAssemblyMatching = options.SimpleAssemblyMatching;
        _binder = options.TypeResolver;
    }

    Type ITypeResolver.BindToType(TypeName typeName)
    {
        if (TryBindToType(typeName, out Type? type))
        {
            return type;
        }

        throw new SerializationException(string.Format(SR.Serialization_MissingType, typeName.AssemblyQualifiedName));
    }

    public bool TryBindToType(TypeName typeName, [NotNullWhen(true)] out Type? type)
    {
        Debug.Assert(typeName.AssemblyName is not null);

        if (_types.TryGetValue(typeName.AssemblyQualifiedName, out Type? cachedType))
        {
            type = cachedType;
            return true;
        }

        if (_binder?.BindToType(typeName) is Type binderType)
        {
            // BinaryFormatter is inconsistent about what caching behavior you get with binders.
            // It would always cache the last item from the binder, but wouldn't put the result
            // in the type cache. This could lead to inconsistent results if the binder didn't
            // always return the same result for a given set of strings. Choosing to always cache
            // for performance.

            _types[typeName.AssemblyQualifiedName] = binderType;
            type = binderType;
            return true;
        }

        if (!_assemblies.TryGetValue(typeName.AssemblyName.FullName, out Assembly? assembly))
        {
            AssemblyName assemblyName = typeName.AssemblyName.ToAssemblyName();
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch
            {
                if (!_simpleAssemblyMatching)
                {
                    throw;
                }

                assembly = Assembly.Load(assemblyName.Name!);
            }

            _assemblies.Add(typeName.AssemblyName.FullName, assembly);
        }

        type = _simpleAssemblyMatching
            ? GetSimplyNamedTypeFromAssembly(assembly, typeName)
            : assembly.GetType(typeName.FullName);

        if (type is not null)
        {
            _types[typeName.AssemblyQualifiedName] = type;
            return true;
        }

        return false;
    }

    private static Type? GetSimplyNamedTypeFromAssembly(Assembly assembly, TypeName typeName)
    {
        // Catching any exceptions that could be thrown from a failure on assembly load
        // This is necessary, for example, if there are generic parameters that are qualified
        // with a version of the assembly that predates the one available.

        try
        {
            return assembly.GetType(typeName.FullName, throwOnError: false, ignoreCase: false);
        }
        catch (TypeLoadException) { }
        catch (FileNotFoundException) { }
        catch (FileLoadException) { }
        catch (BadImageFormatException) { }

        return Type.GetType(
            typeName.FullName,
            ResolveSimpleAssemblyName,
            new TopLevelAssemblyTypeResolver(assembly).ResolveType,
            throwOnError: false);

        static Assembly? ResolveSimpleAssemblyName(AssemblyName assemblyName)
        {
            try
            {
                return Assembly.Load(assemblyName);
            }
            catch { }

            try
            {
                return Assembly.Load(assemblyName.Name!);
            }
            catch { }

            return null;
        }
    }

    private sealed class TopLevelAssemblyTypeResolver
    {
        private readonly Assembly _topLevelAssembly;

        public TopLevelAssemblyTypeResolver(Assembly topLevelAssembly) => _topLevelAssembly = topLevelAssembly;

        [RequiresUnreferencedCode("Calls System.Reflection.Assembly.GetType(String, Boolean, Boolean)")]
        public Type? ResolveType(Assembly? assembly, string simpleTypeName, bool ignoreCase)
        {
            assembly ??= _topLevelAssembly;
            return assembly.GetType(simpleTypeName, throwOnError: false, ignoreCase);
        }
    }
}
