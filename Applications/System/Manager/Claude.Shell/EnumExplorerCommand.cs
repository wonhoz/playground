using System.Runtime.InteropServices;

namespace Claude.Shell
{
    [ComVisible(false)]
    internal sealed class EnumExplorerCommand : IEnumExplorerCommand
    {
        private readonly IExplorerCommand[] _items;
        private int _index;

        internal EnumExplorerCommand(IExplorerCommand[] items, int startIndex = 0)
        {
            _items = items;
            _index = startIndex;
        }

        public int Next(uint celt, out IExplorerCommand pUICommand, out uint pceltFetched)
        {
            if (_index < _items.Length)
            {
                pUICommand = _items[_index++];
                pceltFetched = 1;
                return HR.S_OK;
            }
            pUICommand = null;
            pceltFetched = 0;
            return HR.S_FALSE;
        }

        public int Skip(uint celt)  { _index += (int)celt; return HR.S_OK; }
        public int Reset()          { _index = 0;           return HR.S_OK; }

        public int Clone(out IEnumExplorerCommand ppenum)
        {
            ppenum = new EnumExplorerCommand(_items, _index);
            return HR.S_OK;
        }
    }
}
