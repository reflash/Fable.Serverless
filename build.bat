cd FableApp/src 
dotnet build
cd ../../

cd FableApp 
call npm install
cd ./../

cd FableApp
call npm run-script build
cd ../

dotnet build FunctionApp