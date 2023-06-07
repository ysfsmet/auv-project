# AUV Project
* Project presentation is ready in docs.
* "AUV Static Environment Simulation" can be run directly in releases.
* To create Unity Environment to train and simulation the Agent, please follow the instructions below.

## Prerequests

1. Install [Python 3.9.x]
1. Install [Unity Editor]
1. [Create a virtual environment](#virtual-environment-windows)

## Virtual Environment (Windows)

Run the following commands.

```sh
$ python -m venv sample-env
$ .\sample-env\Scripts\activate
$ python -m pip install --upgrade pip
$ pip install -r requirements_py39.txt
```

Note that on Windows, you may also need Microsoft's [Visual C++ Redistributable](https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads) if you don't have it already. See the [PyTorch installation guide](https://pytorch.org/get-started/locally/) for more installation options and versions.

[Unity Editor]: https://unity.com/download?currency=EUR
[Python 3.9.x]: https://www.python.org/ftp/python/3.9.0/python-3.9.0-amd64.exe

## Add Environment Project

### Project Import Steps

1. Open Unity Hub.
2. Select `Open` and choose `AUV` folder.
3. Ignore warnings and choose `Continue`
4. Start with `Safety Mode`
5. Open the `Package Manager` under `Window` Menu

![package manager][package-manager]
 
6. Click the `+` button and select `Add package from disk..` option
7. Locate to `com.unity.ml-agents` under root. Select `package.json`
8. Apply same steps for `com.unity.ml-agents-extensions`

### Setup Scene

1. Double click to SampleScene object under the Scenes folder.

   ![open scene][open-scene] 


[open-scene]: resources/open-scene.png
[package-manager]: resources/package-manager.png
