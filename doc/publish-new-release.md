# Publish a new release

1. Update `Package version` of `OneNoteMdExporter` project to the desired software version.
3. Ensure PanDoc archive has beed unzipped in folder `src\OneNoteMdExporter\pandoc`
3. From Visual Studio, use Publish profile of `OneNoteMdExporter` project to build the tool binaries
4. Open output folder (`bin\Release\net10.0\win-x64\publish`) and Zip the content of the folder and name it `OneNoteMdExporter.vX.Y.Z.zip`
5. Go into Releases, create a tag "vX.Y.Z" and create a release "vX.Y.Z"