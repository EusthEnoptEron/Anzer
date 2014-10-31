This NuGet package must be compiled and configured for x86 platforms.
Open the configuration manager dialog and set your project platform to x86.
After that, the application can run on both x86 and x64 platforms.
You can also download the x64 assemblies version from our site:
http://www.imagecomponents.net/Download.aspx
CSharp:
	- Right click your project and select 'properties'.
	- Select the Build tab.
	- Change the value of the 'Platform target' to X86.
	- Rebuild your project.
VB.Net:
	- Right click your project and select 'properties'.
	- Select the Compile tab.
	- Change the value of the 'Target CPU' to X86.
	- Rebuild your project.

Running the example files:
	- Instantiate your language choice example class(ICWin32CSharp.cs or ICWin32VB.vb):
		CSharp:
				ICWin32Example.CSharpExample m_Example = new ICWin32Example.CSharpExample();
				m_Example.Initialize();
		VB.Net:
				Dim m_Example As ICWin32Example.VBExample = New ICWin32Example.VBExample()
				m_Example.Initialize()

Adding the components to the Visual Studio Toolbox:
http://www.imagecomponents.net/Win32/QuickStarts/ICWin32Toolbox.aspx

Source example files included:
 - ICWin32CSharp.cs - CSharp example
 - ICWin32VB.vb - VB.Net example