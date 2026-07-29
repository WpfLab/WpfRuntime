using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

#if WINDOWS_BASE
namespace MS.Internal.Interop;
#elif UIAUTOMATIONTYPES                  
using MS.Internal.UIAutomationTypes;
#endif

[ComImport(), Guid("618736E0-3C3D-11CF-810C-00AA00389B71"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAccessible
{
}
