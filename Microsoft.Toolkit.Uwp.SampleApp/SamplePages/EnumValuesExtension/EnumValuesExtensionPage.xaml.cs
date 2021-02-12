// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.SampleApp.Enums;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Microsoft.Toolkit.Uwp.SampleApp.SamplePages
{
    /// <summary>
    /// A page that shows how to use the <see cref="EnumValuesExtension"/> type.
    /// </summary>
    public sealed partial class EnumValuesExtensionPage : IXamlRenderListener
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EnumValuesExtensionPage"/> class.
        /// </summary>
        public EnumValuesExtensionPage()
        {
            InitializeComponent();
        }

        public void OnXamlRendered(FrameworkElement control)
        {
        }
    }
}

#pragma warning disable SA1403 // File may only contain a single namespace
namespace Microsoft.Toolkit.Uwp.SampleApp.Enums
{
    public enum Animal
    {
        Cat,
        Dog,
        Bunny,
        Parrot,
        Squirrel
    }
}

#pragma warning restore SA1403
