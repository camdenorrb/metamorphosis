using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// this is a Windows-only Revit addin; GenerateAssemblyInfo is off, so declare the platform here.
[assembly: SupportedOSPlatform("windows7.0")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("997fca6e-9ee3-48b0-8deb-30149ba1a88b")]

// Make this assembly visible to our friend the Dynamo node!
[assembly:InternalsVisibleTo("MetamorphosisDynamo")]
