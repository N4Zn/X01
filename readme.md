
HOW TO ADD new master
+ Create csv file, put it in Assets/MasterData/Csv/
+ Create csv master class in Assets/MasterData/CsvClass (refer implementation of existing master class)
+ Create Importer class for new master in Assets/Editor/CSVImporter (refer existing importer class)
+ Add the master class to the masterData Group to load (eg in StartMainController.cs)
+ Run steps in HOW TO CREATE/UPDATE master data bundle (as bellow)
+ Add LoadCsv for new master data in LoadCsvDataForEditor to load master data directly when using Editor (method name = Load + MasterData name)
+ Test in game


HOW TO CREATE/UPDATE master data bundle
+ update csv in MasterData/Csv
+ open editor to generate scriptable object in MasterData/Data
+ Copy scriptable object files to Assets/game_assets then run Menu > AssetBundleBuild > Build MasterData AssetBundles

cp ./Assets/MasterData/Data/* ./Assets/game_assets/MasterData/
+ Copy the output files in proj folder/AssetsBundles to file servers and Assets/StreamingAssets folder

cp -r ./AssetBundles/Android ./Assets/StreamingAssets
(chang use iOS for ios build)



FOR PLANNER:
+ After update csv master data file in Assets/MasterData/Csv, run scene StartScenePlanner to load csv data directly instead of bundle
When Finish updating, build master data bundle as above steps