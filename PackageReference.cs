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

namespace Microsoft.OneGet.Plugin.Chocolatey {
    internal class PackageReference {
        internal IPackage Package {get; set;}
        internal string Source {get; set;}
        internal string FastPath {get; set;}

        internal bool IsPackageFile {get; set;}

        internal string Id {
            get {
                return Package.Id;
            }
        }

        internal string Version {
            get {
                return Package.Version.ToString();
            }
        }
    }
}