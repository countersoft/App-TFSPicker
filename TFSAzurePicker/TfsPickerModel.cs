using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSAzurePicker
{
    public class TfsPickerModel
    {
        public Dictionary<string, TfsPickerItem> TfsPickModel { get; set; }
    }

    public class TfsPickerItem
    {
        public int Id { get; set; }
        public string TypeName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ProjectName { get; set; }
    }
}
