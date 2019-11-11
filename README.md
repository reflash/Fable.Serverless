# Weather.Fable.Serverless

This is reworked sample of https://github.com/pauldorehill/Fable.Serverless

### Install the Azure Functions Core Tools package
```
npm install -g azure-functions-core-tools
```

### local.settings.json

Fill in the required data in local.settings.json (use local.settings.template.json as an example)

### Build the project with

```
sh build.sh 
```
(for Windows)
```
build.bat
```

### Run the serverless functions locally with
```
cd FunctionApp; func start
```

### Build + Run
Build and start the function app running locally in one go
```
sh run.sh 
```
(for Windows)
```
run.bat
```