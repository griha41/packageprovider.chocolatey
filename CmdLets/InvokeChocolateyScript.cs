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

namespace OneGet.PackageProvider.Chocolatey.CmdLets {
    using System;
    using System.Management.Automation;
    using Microsoft.OneGet;
    using Microsoft.OneGet.Core.Extensions;

    /// <summary>
    ///     I'm not entirely convinced this is the best way to handle this, but
    ///     it'll have to do for now.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ChocolateyScript")]
    public class InvokeChocolateyScript : AsyncCmdlet {
        [Parameter(ValueFromRemainingArguments = true)]
        public string[] Arguments;

        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "UseUnencodedScript")]
        public string Script {get; set;}

        [Parameter(Mandatory = true, ParameterSetName = "UseBase64Script")]
        public string Base64Script {get; set;}

        public override bool ProcessRecordAsync() {
            var scr = string.IsNullOrEmpty(Base64Script) ? Script : Base64Script.FromBase64();
            try {
                using (var state = new ChocolateyState(Invoke)) {
                    var script = new ChocolateyScript(state);
                    script.InvokeChocolateyScript(scr, Arguments);
                }
            } catch (Exception e) {
                e.Dump();
            }
            return true;
        }
    }
}