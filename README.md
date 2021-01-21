# HttpFuzzer
Project for Computer Security 2021 

## To run
### Server:
1. Install the [Primary Requisite Software](#primary-requisites) and add to PATH variable.
2. Install the requisite server software _for stub app you wish to run_
	- See [Apps](./Apps) folders for what stubs are available
	- See READMEs inside individual stub apps folders for requisite software/setup for individual stubs
3. `cd` into the [Harness](./Harness) folder
```
λ dotnet run [StubFolderName]
-- or --
λ dotnet watch run [StubFolderName]
```
The folder being run should include a file called `spec.json`, which tells the harness what commands to build/run the project.  
"build" is a misnomer, and could be considered "prepare", as the provided command should download any packages needed.  
"run" should just run the application.  

See the [Example Spec](./Harness/spec-example.json) file for a well-formed reference.

### Fuzzer:  
1. install the [Primary Requisite Software](#primary-requisites) and add to PATH variable.  
2. `cd` into the [Fuzzer](./Fuzzer) folder  
```
λ dotnet run [TargetIP|SchemaFile]
-- or --
λ dotnet watch run [TargetIP|SchemaFile]
```

## Primary Requisites:

### .NET Core
https://dotnet.microsoft.com/download/dotnet-core
Latest downloads:
https://dotnet.microsoft.com/download/dotnet/5.0

As of writing, Developing on V 5.0.1  
V 5.0.2 is out and should be compatible.  
Using the `dotnet-install` script should automatically add to PATH.

After install, be sure to test and make sure the command works.  
```
λ dotnet --version
5.0.101
```
Try opening a new console/session and running the command again if it does not.
