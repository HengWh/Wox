// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Wox.Plugin.Indexer
{
    public class IndexerSettings
    {
        public int MaxSearchCount { get; set; } = 60;
        public int BaseScore { get; set; } = 100;

        public bool UseLocationAsWorkingDir { get; set; }
    }
}
