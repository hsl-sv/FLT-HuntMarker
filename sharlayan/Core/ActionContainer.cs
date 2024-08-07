// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ActionContainer.cs" company="SyndicatedLife">
//   Copyrightę 2007 - 2022 Ryan Wilson <syndicated.life@gmail.com> (https://syndicated.life/)
//   Licensed under the MIT license. See LICENSE.md in the solution root for full license information.
// </copyright>
// <summary>
//   ActionContainer.cs Implementation
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Sharlayan.Core {
    using System.Collections.Generic;

    using Sharlayan.Core.Enums;
    using Sharlayan.Core.Interfaces;

    public class ActionContainer : IActionContainer {
        public List<ActionItem> ActionItems { get; } = new List<ActionItem>();

        public Action.Container ContainerType { get; set; }
    }
}