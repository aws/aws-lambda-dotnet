using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.Tools.Commands
{
    /// <summary>
    /// Interface for commands to implement.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// If enabled the tool will prompt for required fields if they are not already given.
        /// </summary>
        bool DisableInteractive { get; set; }

        Task<bool> ExecuteAsync();
    }
}
