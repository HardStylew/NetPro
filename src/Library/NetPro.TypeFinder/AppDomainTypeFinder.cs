﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;

namespace System.NetPro
{
    /// <summary>
    ///应用程序域内 循环类型查找(在bin目录中)
    /// </summary>
    public class AppDomainTypeFinder : ITypeFinder
    {
        #region Fields

        private readonly bool _ignoreReflectionErrors = true;
        Dictionary<string, string> loadedDll = new Dictionary<string, string>();
        private static HashSet<Assembly> _CustomeAssembliesHashSet = new HashSet<Assembly>();

        private readonly INetProFileProvider _fileProvider;
        private readonly TypeFinderOption _typeFinderOption;

        #endregion

        #region Ctor

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileProvider"></param>
        /// <param name="typeFinderOption"></param>
        public AppDomainTypeFinder(INetProFileProvider fileProvider, TypeFinderOption typeFinderOption)
        {
            _fileProvider = fileProvider;// ?? CoreHelper.DefaultFileProvider;
            _typeFinderOption = typeFinderOption;

        }

        #endregion

        #region Utilities

        /// <summary>
        /// Iterates all assemblies in the AppDomain and if it's name matches the configured patterns add it to our list.
        /// </summary>
        /// <param name="addedAssemblyNames"></param>
        /// <param name="assemblies"></param>
        private void AddAssembliesInAppDomain(List<string> addedAssemblyNames, List<Assembly> assemblies)
        {
            //AssemblyLoadContext.Default.Assemblies
            var currentAssemblies = AssemblyLoadContext.Default.Assemblies;//AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in currentAssemblies)
            {
                if (!Matches(assembly.FullName))
                    continue;

                if (addedAssemblyNames.Contains(assembly.FullName))
                    continue;

                assemblies.Add(assembly);
                addedAssemblyNames.Add(assembly.FullName);
            }
        }

        /// <summary>
        /// Adds specifically configured assemblies.
        /// </summary>
        /// <param name="addedAssemblyNames"></param>
        /// <param name="assemblies"></param>
        protected virtual void AddConfiguredAssemblies(List<string> addedAssemblyNames, List<Assembly> assemblies)
        {
            foreach (var assemblyName in AssemblyNames)
            {
                var assembly = Assembly.Load(assemblyName);
                if (addedAssemblyNames.Contains(assembly.FullName))
                    continue;

                assemblies.Add(assembly);
                addedAssemblyNames.Add(assembly.FullName);
            }
        }

        /// <summary>
        /// Check if a dll is one of the shipped dlls that we know don't need to be investigated.
        /// </summary>
        /// <param name="assemblyFullName">
        /// The name of the assembly to check.
        /// </param>
        /// <returns>
        /// True if the assembly should be loaded into NetPro.
        /// </returns>
        public virtual bool Matches(string assemblyFullName)
        {
            return !Matches(assemblyFullName, AssemblySkipLoadingPattern)
                   && Matches(assemblyFullName, AssemblyRestrictToLoadingPattern);
        }

        /// <summary>
        /// Check if a dll is one of the shipped dlls that we know don't need to be investigated.
        /// </summary>
        /// <param name="assemblyFullName">
        /// The assembly name to match.
        /// </param>
        /// <param name="pattern">
        /// The regular expression pattern to match against the assembly name.
        /// </param>
        /// <returns>
        /// True if the pattern matches the assembly name.
        /// </returns>
        protected virtual bool Matches(string assemblyFullName, string pattern)
        {
            return Regex.IsMatch(assemblyFullName, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>
        /// Makes sure matching assemblies in the supplied folder are loaded in the app domain.
        /// </summary>
        /// <param name="mountePath">
        /// plugin path
        /// </param>
        /// <param name="binPath">
        /// The physical path to a directory containing dlls to load in the app domain and plugin directory.
        /// </param>
        protected virtual void LoadMatchingAssemblies(string mountePath, string binPath)
        {
            string entryPoint = null;

            var customAssembly = GetAssemblies();
            foreach (var a in customAssembly)
            {
                if (a.EntryPoint != null)
                {
                    entryPoint = a.EntryPoint.Module.Assembly.GetName().Name;
                }
                loadedDll[a.GetName().Name] = a.Location;
            }

            //load root sub directory
            if (!_fileProvider.DirectoryExists($"{mountePath}/{entryPoint}"))
            {
                _fileProvider.CreateDirectory($"{mountePath}/{entryPoint}");
                _fileProvider.WriteAllText($"{mountePath}/readme.text", @"This directory contains DLLs, and the system will retrieve the DLLS in the current directory ", Encoding.UTF8);
            }

            _fileProvider.Move(mountePath, $"{binPath}", false);

            //load bin directory
            _LoadDll(binPath);
        }

        private void _LoadDll(string directory)
        {
            _(directory);
            var subDirectories = _fileProvider.GetDirectories(directory).Where(s => s != "runtimes").ToList();
            for (int i = 0; i < subDirectories.Count(); i++)
            {
                _(subDirectories[i]);
            }

            void _(string _directory)
            {
                var currentAssemblies = AssemblyLoadContext.Default.Assemblies;
                var dllFiles = _fileProvider.GetFiles(_directory, "*.dll", true);
                foreach (var dllPath in dllFiles)
                {
                    try
                    {
                        var an = AssemblyName.GetAssemblyName(dllPath);
                        if (currentAssemblies.Where(s => s.GetName().Name != an.Name).Any() && !loadedDll.ContainsKey(an.Name))
                        {
                            //https://cloud.tencent.com/developer/article/1520894
                            //https://cloud.tencent.com/developer/article/1581619?from=article.detail.1520894
                            AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
                            //Domain.LoadPlugin(dllPath);
                            loadedDll[an.Name] = dllPath;
                            //App.Load(an);
                        }
                        if (loadedDll.ContainsKey(an.Name) || "Microsoft.CodeAnalysis.resources".Contains(an.Name))
                        {
                            try
                            {
                                _fileProvider.DeleteFile(dllPath);
                            }
                            catch (Exception)
                            {
                            }
                            continue;
                        }
                    }
                    catch (BadImageFormatException ex)
                    {
                        Trace.TraceError(ex.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Does type implement generic?
        /// </summary>
        /// <param name="type"></param>
        /// <param name="openGeneric"></param>
        /// <returns></returns>
        protected virtual bool DoesTypeImplementOpenGeneric(Type type, Type openGeneric)
        {
            try
            {
                var genericTypeDefinition = openGeneric.GetGenericTypeDefinition();
                foreach (var implementedInterface in type.FindInterfaces((objType, objCriteria) => true, null))
                {
                    if (!implementedInterface.IsGenericType)
                        continue;

                    if (genericTypeDefinition.IsAssignableFrom(implementedInterface.GetGenericTypeDefinition()))
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Find classes of type
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="onlyConcreteClasses">A value indicating whether to find only concrete classes</param>
        /// <returns>Result</returns>
        public IEnumerable<Type> FindClassesOfType<T>(bool onlyConcreteClasses = true)
        {
            return FindClassesOfType(typeof(T), onlyConcreteClasses);
        }

        /// <summary>
        /// Find classes of type
        /// </summary>
        /// <param name="assignTypeFrom">Assign type from</param>
        /// <param name="onlyConcreteClasses">A value indicating whether to find only concrete classes</param>
        /// <returns>Result</returns>
        /// <returns></returns>
        public IEnumerable<Type> FindClassesOfType(Type assignTypeFrom, bool onlyConcreteClasses = true)
        {
            return FindClassesOfType(assignTypeFrom, GetAssemblies(), onlyConcreteClasses);
        }

        /// <summary>
        /// Find classes of type
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="assemblies">Assemblies</param>
        /// <param name="onlyConcreteClasses">A value indicating whether to find only concrete classes</param>
        /// <returns>Result</returns>
        public IEnumerable<Type> FindClassesOfType<T>(IEnumerable<Assembly> assemblies, bool onlyConcreteClasses = true)
        {
            return FindClassesOfType(typeof(T), assemblies, onlyConcreteClasses);
        }

        /// <summary>
        /// Find classes of type
        /// </summary>
        /// <param name="assignTypeFrom">Assign type from</param>
        /// <param name="assemblies">Assemblies</param>
        /// <param name="onlyConcreteClasses">A value indicating whether to find only concrete classes</param>
        /// <returns>Result</returns>
        public IEnumerable<Type> FindClassesOfType(Type assignTypeFrom, IEnumerable<Assembly> assemblies, bool onlyConcreteClasses = true)
        {
            var result = new List<Type>();
            try
            {
                foreach (var a in assemblies)
                {
                    Type[] types = null;
                    try
                    {
                        types = a.GetTypes();
                    }
                    catch
                    {
                        //Entity Framework 6 doesn't allow getting types (throws an exception)
                        if (!_ignoreReflectionErrors)
                        {
                            throw;
                        }
                    }

                    if (types == null)
                        continue;

                    foreach (var t in types)
                    {
                        if (!assignTypeFrom.IsAssignableFrom(t) && (!assignTypeFrom.IsGenericTypeDefinition || !DoesTypeImplementOpenGeneric(t, assignTypeFrom)))
                            continue;

                        if (t.IsInterface)
                            continue;

                        if (onlyConcreteClasses)
                        {
                            if (t.IsClass && !t.IsAbstract)
                            {
                                result.Add(t);
                            }
                        }
                        else
                        {
                            result.Add(t);
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                var msg = string.Empty;
                foreach (var e in ex.LoaderExceptions)
                    msg += e.Message + Environment.NewLine;

                var fail = new Exception(msg, ex);
                Debug.WriteLine(fail.Message, fail);

                throw fail;
            }

            return result;
        }

        /// <summary>
        /// Gets the assemblies related to the current implementation.
        /// </summary>
        /// <returns>A list of assemblies</returns>
        public virtual IList<Assembly> GetAssemblies()
        {
            //var assemblies = new List<Assembly>();

            //** 获取自定义的程序集
            var currentAssemblies = AssemblyLoadContext.Default.Assemblies;//AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in currentAssemblies)
            {
                if (string.IsNullOrWhiteSpace(_typeFinderOption.CustomDllPattern))
                {
                    if (Matches(assembly.GetName().Name))
                        continue;
                }
                else
                {
                    if (!Matches(assembly.GetName().Name, _typeFinderOption.CustomDllPattern)
                        && !Matches(assembly.GetName().Name, "^NetPro.*"))
                    {
                        continue;
                    }
                }
                //add custome assembly
                _CustomeAssembliesHashSet.Add(assembly);
                
                //assemblies.Add(assembly);
            }
            var result = _CustomeAssembliesHashSet.ToList();
            return result;
        }

        #endregion

        #region Properties

        /// <summary>The app domain to look for types in.</summary>
        ///  AssemblyLoadContext.Default
        //public virtual AppDomain App => AppDomain.CurrentDomain;

        //public virtual NatashaAssemblyDomain Domain => new NatashaAssemblyDomain("Default");//The system must be in Default  

        /// <summary>Gets or sets whether NetPro should iterate assemblies in the app domain when loading NetPro types. Loading patterns are applied when loading these assemblies.</summary>
        public bool LoadAppDomainAssemblies { get; set; } = true;

        /// <summary>Gets or sets assemblies loaded a startup in addition to those loaded in the AppDomain.</summary>
        public IList<string> AssemblyNames { get; set; } = new List<string>();

        /// <summary>Gets the pattern for dlls that we know don't need to be investigated.</summary>
        public string AssemblySkipLoadingPattern { get; set; } = "^BetterConsole|^Com.Ctrip.Framework.Apollo|^Figgle|^netstandard|^Serilog|^System|^mscorlib|^Microsoft|^AjaxControlToolkit|^Antlr3|^Autofac|^AutoMapper|^Castle|^ComponentArt|^CppCodeProvider|^DotNetOpenAuth|^EntityFramework|^EPPlus|^FluentValidation|^ImageResizer|^itextsharp|^log4net|^MaxMind|^MbUnit|^MiniProfiler|^Mono.Math|^MvcContrib|^Newtonsoft|^NHibernate|^nunit|^Org.Mentalis|^PerlRegex|^QuickGraph|^Recaptcha|^Remotion|^RestSharp|^Rhino|^Telerik|^Iesi|^TestDriven|^TestFu|^UserAgentStringLibrary|^VJSharpCodeProvider|^WebActivator|^WebDev|^WebGrease";

        /// <summary>Gets or sets the pattern for dll that will be investigated. For ease of use this defaults to match all but to increase performance you might want to configure a pattern that includes assemblies and your own.</summary>
        /// <remarks>If you change this so that NetPro assemblies aren't investigated (e.g. by not including something like "^NetPro|..." you may break core functionality.</remarks>
        public string AssemblyRestrictToLoadingPattern { get; set; } = ".*";

        #endregion

    }
}
