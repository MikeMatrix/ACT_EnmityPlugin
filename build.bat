@echo off

if not exist "%~dp0\Thirdparty\ACT\Advanced Combat Tracker.exe" (
	echo �G���[: "Thirdparty" �f�B���N�g���� "Advanced Combat Tracker.exe" ���R�s�[���Ă��������B
	goto END
)

if not exist "%~dp0\Thirdparty\OverlayPlugin\OverlayPlugin.Core.dll" (
	echo �G���[: "Thirdparty\OverlayPlugin" �f�B���N�g���� "OverlayPlugin.Core.dll" ���R�s�[���Ă��������B
	goto END
)

if not exist "%~dp0\Thirdparty\OverlayPlugin\OverlayPlugin.Common.dll" (
	echo �G���[: "Thirdparty\OverlayPlugin" �f�B���N�g���� "OverlayPlugin.Common.dll" ���R�s�[���Ă��������B
	goto END
)

if not exist "%~dp0\Thirdparty\OverlayPlugin\HtmlRenderer.dll" (
	echo �G���[: "Thirdparty\OverlayPlugin" �f�B���N�g���� "HtmlRenderer.dll" ���R�s�[���Ă��������B
	goto END
)

set DOTNET_PATH=%windir%\Microsoft.NET\Framework\v4.0.30319
if not exist %DOTNET_PATH% (
	echo �G���[: .NET Framework �̃f�B���N�g����������܂���B�r���h�����s���邽�߂ɂ� .NET Framework 4.5.1 ���C���X�g�[������Ă���K�v������܂��B
	goto END
)


%DOTNET_PATH%\msbuild /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU" /p:OutputPath="%~dp0\Build" "%~dp0\EnmityPlugin.sln"

:END
