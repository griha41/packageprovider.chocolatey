// 
//  Copyright (c) Microsoft Corporation. All rights reserved. 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  

namespace OneGet.PackageProvider.Chocolatey {
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Microsoft.OneGet.Extensions;
    using Callback = System.Object;

    public class ChocolateyPackageProvider {
        public string PackageProviderName {
            get {
                return "Chocolatey";
            }
        }

        public void InitializeProvider(object dynamicInterface, Callback c) {
            DynamicExtensions.RemoteDynamicInterface = dynamicInterface;

            /*
            using (var request = c.As<Request>()) {
                if (NuGet.NuGetCorePath == null) {
                    NuGet.NuGetCorePath = request.GetNuGetDllPath(request);
                }
            }
             * */
        }

        public bool FindPackage(string name, string requiredVersion, string minimumVersion, string maximumVersion, Callback c) {
            // using (var state = ChocolateyRequest.New(c)) {
            using (var request = c.As<Request>()) {
                var allowPrerelease = request.AllowPrereleaseVersions;

                // get the package by ID first.
                // if there are any packages, yield and return
                if (request.YieldPackages(request.GetPackageById(name, requiredVersion, minimumVersion, maximumVersion),name)) {
                    return true;
                }

                // have we been cancelled?
                if (request.IsCancelled()) {
                    return false;
                }

                // Try searching for matches and returning those.
                return request.YieldPackages(request.SearchForPackages(name, requiredVersion, minimumVersion, maximumVersion),name);
            }
        }

        public bool InstallPackage(string fastPath, Callback c) {
            using (var request = c.As<Request>()) {
                var pkgRef = request.GetPackageByFastpath(fastPath);

                if (pkgRef != null) {
                    var dependencies = request.GetUninstalledPackageDependencies(pkgRef).Reverse().ToArray();

                    var n = 0;
                    foreach (var d in dependencies) {
                        // if (!request.WhatIf()) {
                           //  request.Progress(1, (n*100)/dependencies.Length, "Dependency Package '{0}' ({1} of {2})", d.Id, ++n, dependencies.Length);
                        // }
                        if (!request.InstallSingleChocolateyPackage(d)) {
                            request.Error("InstallFailure", "Dependent Package '{0} {1}' not installed", d.Id, d.Version);
                            request.CompleteProgress(1, false);
                            return false;
                        }
                    }
                    // if (!request.WhatIf()) {
                        // request.CompleteProgress(1, true);
                    // }
                    // got this far, let's install the package we came here for.
                    if (!request.InstallSingleChocolateyPackage(pkgRef)) {
                        // package itself didn't install.
                        // roll that back out everything we did install.
                        // and get out of here.
                        request.Error("InstallFailure", "Package '{0}' not installed", pkgRef.Id);
                        return false;
                    }
                    return true;
                }

                // uh, we couldn't find a package for that.
                request.Error("UnknownPackage", "Unable to resolve package {0}", fastPath);
                return false;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Still in development!")]
        public bool GetInstalledPackages(string name, Callback c) {
            if (c == null) {
                throw new ArgumentNullException("c");
            }
            using (var request = c.As<Request>()) {
                var nupkgs = Directory.EnumerateFileSystemEntries(request.PackageInstallationPath, "*.nupkg", SearchOption.AllDirectories);
                foreach (var pkgFile in nupkgs) {
                    if (PackageHelper.IsPackageFile(pkgFile)) {
                        var pkg = new ZipPackage(pkgFile);
                        // todo: wildcard matching?

                        // if this is an exact match, just return that.
                        if (pkg.Id.Equals(name, StringComparison.CurrentCultureIgnoreCase)) {
                            var fastpath = request.MakeFastPath(pkgFile, pkg.Id, pkg.Version.ToString());
                            if (!request.YieldPackage(fastpath, pkg.Id, pkg.Version.ToString(), "semver", pkg.Summary, request.GetNameForSource(pkgFile),name, Path.GetDirectoryName(pkgFile), Path.GetFileName(pkgFile))) {
                                return false;
                            }
                            break;
                        }

                        //otherwise return partial matches.
                        if (string.IsNullOrEmpty(name) || pkg.Id.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) > -1) {
                            var fastpath = request.MakeFastPath(pkgFile, pkg.Id, pkg.Version.ToString());
                            if (!request.YieldPackage(fastpath, pkg.Id, pkg.Version.ToString(), "semver", pkg.Summary, request.GetNameForSource(pkgFile), name, Path.GetDirectoryName(pkgFile), Path.GetFileName(pkgFile))) {
                                return false;
                            }
                        }
                    }
                }
                return true;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Called via remoting.")]
        public bool UninstallPackage(string fastPath, Callback c) {
            if (c == null) {
                throw new ArgumentNullException("c");
            }

            using (var request = c.As<Request>()) {
                return request.UninstallPackage(fastPath, false);
            }
        }

        public bool InstallPackageByFile(string filePath, Callback c) {
            using (var request = c.As<Request>()) {
                filePath = Path.GetFullPath(filePath);
                if (FilesystemExtensions.FileExists(filePath)) {
                    var pkgRef = request.GetPackageByPath(filePath);
                }
            }
            return false;
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Still in development!")]
        internal bool FindPackageByFile(string filePath, Callback c) {
            using (var request = c.As<Request>()) {
                filePath = Path.GetFullPath(filePath);
                if (FilesystemExtensions.FileExists(filePath)) {
                    if (PackageHelper.IsPackageFile(filePath)) {
                        var pkg = new ZipPackage(filePath);
                        var fastPath = request.MakeFastPath(filePath, pkg.Id, pkg.Version.ToString());
                        request.YieldPackage(fastPath, pkg.Id, pkg.Version.ToString(), "semver", "", filePath,filePath);
                    }
                }
            }
            return false;
        }

        // not supported in chocolatey
        // internal bool InstallPackageByUri(string uri, Callback c) {return false;}

        public void GetDynamicOptions(OptionCategory category, Callback c) {
            using (var request = c.As<Request>()) {
                switch (category) {
                    case OptionCategory.Package:
                        if (!request.YieldDynamicOption(OptionCategory.Package, "AllowPrereleaseVersions", OptionType.Switch, false, null)) {
                            return;
                        }
                        if (!request.YieldDynamicOption(OptionCategory.Package, "AllVersions", OptionType.Switch, false, null)) {
                            return;
                        }
                        if (!request.YieldDynamicOption(OptionCategory.Package, "LocalOnly", OptionType.Switch, false, null)) {
                            return;
                        }
                        if (!request.YieldDynamicOption(OptionCategory.Package, "Hint", OptionType.Switch, false, null)) {
                            return;
                        }
                        if (!request.YieldDynamicOption(OptionCategory.Package, "LeavePartialPackageInstalled", OptionType.Switch, false, null)) {
                            return;
                        }
                        break;
                }
            }
        }

        public void AddPackageSource(string name, string location, bool trusted, Callback c) {
            using (var request = c.As<Request>()) {
                if (string.IsNullOrEmpty(name)) {
                    request.Error("Chocolatey Package Sources require parameter", "Name");
                }
                if (string.IsNullOrEmpty(location)) {
                    request.Error("Chocolatey Package Sources require parameter", "Location");
                }
                request.AddPackageSource(name, location, trusted);
            }
        }

        public void RemovePackageSource(string name, Callback c) {
            using (var request = c.As<Request>()) {
                request.RemovePackageSource(name);
            }
        }

        public bool GetPackageSources(Callback c) {
            using (var request = c.As<Request>()) {
                var sources = request.AllPackageRepositories;
                foreach (var k in sources.Keys) {
                    request.YieldPackageSource(k, sources[k].Location, sources[k].Trusted, sources[k].IsRegistered);
                }
            }
            return true;
        }

        public bool IsValidPackageSource(string packageSource, Callback c) {
            using (var request = c.As<Request>()) {
                if (Uri.IsWellFormedUriString(packageSource, UriKind.Absolute)) {
                    var uri = new Uri(packageSource, UriKind.Absolute);
                    switch (uri.Scheme.ToLower(CultureInfo.CurrentCulture)) {
                        case "http":
                        case "https":
                        case "file":
                            // we should really do a quick fetch to see if this is actually a good one.
                            return true;
                    }
                    return false;
                }

                if (Directory.Exists(packageSource)) {
                    return true;
                }
                return request.AllPackageRepositories.ContainsKey(packageSource);
            }
        }

        public bool IsTrustedPackageSource(string packageSource, Callback c) {
            using (var request = c.As<Request>()) {
                var apr = request.AllPackageRepositories;
                if (apr.ContainsKey(packageSource)) {
                    return apr[packageSource].Trusted;
                }
                if (apr.Values.Any(each => each.Trusted && (each.Location.Equals(packageSource, StringComparison.OrdinalIgnoreCase) || each.Location.Equals(packageSource.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))) {
                    return true;
                }

                return false;
            }
        }
    }

    #region copy PackageProvider-types
public enum OptionCategory {
        Package = 0,
        Provider = 1,
        Source = 2,
        Install = 3
    }

    public enum OptionType {
        String = 0,
        StringArray = 1,
        Int = 2,
        Switch = 3,
        Folder = 4,
        File = 5,
        Path = 6,
        Uri = 7,
        SecureString = 8
    }

    public enum EnvironmentContext {
        All = 0,
        User = 1,
        System = 2
    }

    #endregion

}