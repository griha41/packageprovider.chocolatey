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
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Microsoft.OneGet.Core.Extensions;
    using Callback = System.Object;

    public class ChocolateyPackageProvider {
        public ChocolateyPackageProvider() {
        }

        public string PackageProviderName  {
            get {
                return "Chocolatey";
            }
        }

        public bool FindPackage(string name, string requiredVersion, string minimumVersion, string maximumVersion, Callback c) {
            // using (var state = ChocolateyRequest.New(c)) {
            using (var state = Request.New(c)) {
                var allowPrerelease = state.AllowPrereleaseVersions;

                // get the package by ID first.
                // if there are any packages, yield and return
                if (state.YieldPackages(state.GetPackageById(name, requiredVersion, minimumVersion, maximumVersion))) {
                    return true;
                }

                // have we been cancelled?
                if (state.IsCancelled()) {
                    return false;
                }

                // Try searching for matches and returning those.
                return state.YieldPackages(state.SearchForPackages(name, requiredVersion, minimumVersion, maximumVersion));
            }
        }

        public bool InstallPackage(string fastPath, Callback c) {
            using (var state = Request.New(c)) {
                var pkgRef = state.GetPackageByFastpath(fastPath);

                if (pkgRef != null) {
                    var dependencies = state.GetUninstalledPackageDependencies(pkgRef).Reverse().ToArray();

                    var n = 0;
                    foreach (var d in dependencies) {
                        if (!state.WhatIf()) {
                            state.Progress(1, (n*100)/dependencies.Length, "Dependency Package '{0}' ({1} of {2})", d.Id, ++n, dependencies.Length);
                        }
                        if (!state.InstallSingleChocolateyPackage(d)) {
                            state.Error("InstallFailure", "Dependent Package '{0} {1}' not installed", d.Id, d.Version);
                            state.CompleteProgress(1, false);
                            return false;
                        }
                    }
                    if (!state.WhatIf()) {
                        state.CompleteProgress(1,true);
                    }
                    // got this far, let's install the package we came here for.
                    if (!state.InstallSingleChocolateyPackage(pkgRef)) {
                        // package itself didn't install.
                        // roll that back out everything we did install.
                        // and get out of here.
                        state.Error("InstallFailure", "Package '{0}' not installed", pkgRef.Id);
                        return false;
                    }
                    return true;
                }

                // uh, we couldn't find a package for that.
                state.Error("UnknownPackage", "Unable to resolve package {0}", fastPath);
                return false;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Still in development!")]
        public bool GetInstalledPackages(string name, Callback c) {
            if (c == null) {
                throw new ArgumentNullException("c");
            }
            using (var state = Request.New(c)) {
                var nupkgs = Directory.EnumerateFileSystemEntries(state.PackageInstallationPath, "*.nupkg", SearchOption.AllDirectories);
                foreach (var pkgFile in nupkgs) {
                    if (PackageHelper.IsPackageFile(pkgFile)) {
                        var pkg = new ZipPackage(pkgFile);
                        // todo: wildcard matching?

                        // if this is an exact match, just return that.
                        if (pkg.Id.Equals(name, StringComparison.CurrentCultureIgnoreCase)) {
                            var fastpath = state.MakeFastPath(pkgFile, pkg.Id, pkg.Version.ToString());
                            if (!state.YieldPackage(fastpath, pkg.Id, pkg.Version.ToString(), "semver", pkg.Summary, state.GetNameForSource(pkgFile))) {
                                return false;
                            }
                            break;
                        }

                        //otherwise return partial matches.
                        if (string.IsNullOrEmpty(name) || pkg.Id.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) > -1 ) {
                            var fastpath = state.MakeFastPath(pkgFile, pkg.Id, pkg.Version.ToString());
                            if (!state.YieldPackage(fastpath, pkg.Id, pkg.Version.ToString(), "semver", pkg.Summary, state.GetNameForSource(pkgFile))) {
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

            using (var state = Request.New(c)) {
                return state.UninstallPackage(fastPath, false);
            }
        }

        public bool InstallPackageByFile(string filePath, Callback c) {
            using (var state = Request.New(c)) {
                filePath = Path.GetFullPath(filePath);
                if (FilesystemExtensions.FileExists(filePath)) {
                    var pkgRef = state.GetPackageByPath(filePath);
                }
            }
            return false;
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Still in development!")]
        internal bool FindPackageByFile(string filePath, Callback c) {
            using (var state = Request.New(c)) {
                filePath = Path.GetFullPath(filePath);
                if (FilesystemExtensions.FileExists(filePath)) {
                    if (PackageHelper.IsPackageFile(filePath)) {
                        var pkg = new ZipPackage(filePath);
                        var fastPath = state.MakeFastPath(filePath, pkg.Id, pkg.Version.ToString());
                        state.YieldPackage(fastPath, pkg.Id, pkg.Version.ToString(), "semver", "", filePath);
                    }
                }
            }
            return false;
        }

        // not supported in chocolatey
        // internal bool InstallPackageByUri(string uri, Callback c) {return false;}

        public void GetDynamicOptions(OptionCategory category, Callback c) {
            using (var state = Request.New(c)) {

                switch (category) {
                    case OptionCategory.Package:
                        if (!state.YieldDynamicOption((int)OptionCategory.Package, "AllowPrereleaseVersions", (int)OptionType.Switch, false, null)) {
                            return;
                        }
                        if (!state.YieldDynamicOption((int)OptionCategory.Package, "AllVersions", (int)OptionType.Switch, false, null)) {
                            return;
                        }
                        if (!state.YieldDynamicOption((int)OptionCategory.Package, "LocalOnly", (int)OptionType.Switch, false, null)) {
                            return;
                        }
                        if (!state.YieldDynamicOption((int)OptionCategory.Package, "Hint", (int)OptionType.Switch, false, null)) {
                            return;
                        }
                        if (!state.YieldDynamicOption((int)OptionCategory.Package, "LeavePartialPackageInstalled", (int)OptionType.Switch, false, null)) {
                            return;
                        }
                        break;
                }
            }
        }

        public void AddPackageSource(string name, string location, bool trusted, Callback c) {
            using (var state = Request.New(c)) {
                if (string.IsNullOrEmpty(name)) {
                    state.Error("Chocolatey Package Sources require parameter", "Name");
                }
                if (string.IsNullOrEmpty(location)) {
                    state.Error("Chocolatey Package Sources require parameter", "Location");
                }
                state.AddPackageSource(name, location, trusted);
            }
        }

        public void RemovePackageSource(string name, Callback c) {
            using (var state = Request.New(c)) {
                state.RemovePackageSource(name);
            }
        }

        public bool GetPackageSources(Callback c) {
            using (var state = Request.New(c)) {
                var sources = state.AllPackageRepositories;
                foreach (var k in sources.Keys) {
                    state.YieldSource(k, sources[k].Location, sources[k].Trusted);
                }
            }
            return true;
        }

        public bool IsValidPackageSource(string packageSource, Callback c) {
            using (var state = Request.New(c)) {
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
                return state.AllPackageRepositories.ContainsKey(packageSource);
            }
        }

        public bool IsTrustedPackageSource(string packageSource, Callback c) {
            using (var state = Request.New(c)) {
                var apr = state.AllPackageRepositories;
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
        Path = 4,
        Uri = 5
    }

    #endregion
}