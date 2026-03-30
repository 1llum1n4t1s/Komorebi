Remove-Item -Path build\Komorebi\*.pdb -Force
Compress-Archive -Path build\Komorebi -DestinationPath "build\komorebi_${env:VERSION}.${env:RUNTIME}.zip" -Force