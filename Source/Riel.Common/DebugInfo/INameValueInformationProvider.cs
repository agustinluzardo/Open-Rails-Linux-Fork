using System.Collections.Generic;

namespace Riel.Common.DebugInfo
{

    public interface INameValueInformationProvider
    {
        public InformationDictionary DetailInfo { get; }

        public Dictionary<string, FormatOption> FormattingOptions { get; }
    }
}
