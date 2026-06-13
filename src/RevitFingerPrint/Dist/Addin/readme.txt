Instructions:
Build the Release configuration of src/Metamorphosis.sln, then copy the output
(Metamorphosis.dll plus its dependencies) into this folder's Metamorphosis subfolder,
and copy this folder into the C:\ProgramData\Autodesk\Revit\Addins folder.

Note: pre-built DLLs are intentionally not committed to this repository. The previously
committed binaries were stale and bundled an outdated Newtonsoft.Json (9.0.1) with a
known vulnerability. Always build from source or use the official signed installer.
