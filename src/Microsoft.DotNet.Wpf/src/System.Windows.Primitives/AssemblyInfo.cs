// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Internal;

[assembly: CLSCompliant(false)]

[assembly: InternalsVisibleTo($"PresentationBuildTasks, PublicKey={BuildInfo.WCP_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"PresentationCore, PublicKey={BuildInfo.WCP_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"PresentationFramework, PublicKey={BuildInfo.WCP_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"PresentationUI, PublicKey={BuildInfo.WCP_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"ReachFramework, PublicKey={BuildInfo.WCP_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"System.Windows.Controls.Ribbon, PublicKey={BuildInfo.DEVDIV_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"System.Windows.Input.Manipulations, PublicKey={BuildInfo.DEVDIV_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"System.Windows.Presentation, PublicKey={BuildInfo.DEVDIV_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"System.Xaml, PublicKey={BuildInfo.DEVDIV_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"UIAutomationClient, PublicKey={BuildInfo.WCP_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"UIAutomationClientSideProviders, PublicKey={BuildInfo.DEVDIV_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"UIAutomationProvider, PublicKey={BuildInfo.WCP_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"UIAutomationTypes, PublicKey={BuildInfo.WCP_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"WindowsBase, PublicKey={BuildInfo.WCP_PUBLIC_KEY_STRING}")]
[assembly: InternalsVisibleTo($"WindowsFormsIntegration, PublicKey={BuildInfo.WCP_PUBLIC_KEY_STRING}")]