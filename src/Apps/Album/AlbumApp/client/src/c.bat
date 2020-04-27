cd D:\Projects\PersonalCloud\src
del Test\TestConsoleApp\bin\Debug\netcoreapp3.1\Apps\Static\Album\*  /Q /S /F
xcopy /E PersonalCloud\Apps\Album\client\build\* Test\TestConsoleApp\bin\Debug\netcoreapp3.1\Apps\Static\Album\
