#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Ubicomp.Utils.NET.ContextAwarenessFramework.DataModel
{

    /// <summary>
    /// Defines a context entity that can notify listeners when its properties
    /// change.
    /// </summary>
    public interface IEntity : INotifyPropertyChanged
    {
        /// <summary>Gets or sets the unique identifier for the
        /// entity.</summary>
        Guid EntityGuid { get; set; }
    }

}
