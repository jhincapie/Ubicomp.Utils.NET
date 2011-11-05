using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace CAF.DataModel
{

  public interface IEntity : INotifyPropertyChanged
  {
    Guid EntityGuid
    { get; set; }
  }

}
