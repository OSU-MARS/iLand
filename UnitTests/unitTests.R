# maintenance code for unit tests
library(arrow)

elliottUnitTestRUweather = read_feather("UnitTests/Elliott/gis/unit test resource units 200 m weather.feather", mmap = FALSE)
elliott4kResourceUnitsWeather = read_feather("UnitTests/Elliott/gis/resource units unbuffered 4 km weather.feather", mmap = FALSE)

tSegD_H10Cr20h10A50MD7_s04110w07020 = read_feather("UnitTests/Elliott/init/TSegD_H10Cr20h10A50MD7_s04110w07020.feather", mmap = FALSE)
tSegD_H10Cr20h10A50MD7_s04110w07050 = read_feather("UnitTests/Elliott/init/TSegD_H10Cr20h10A50MD7_s04110w07050.feather", mmap = FALSE)

# compress CO and LIPs
elliottCO2 = read_feather("UnitTests/Elliott/database/co2 ssp370.feather", mmap = FALSE)
elliottCO2feather = arrow_table(elliottCO2, schema = schema(year = int16(), month = uint8(), co2 = float32()))
write_feather(elliottCO2feather, "UnitTests/Elliott/database/co2 ssp370.feather")

# pacificNorthwestLipFiles = list.files("UnitTests/Elliott/lip", pattern = "\\.feather$")
# for (pacificNorthwestLipFile in pacificNorthwestLipFiles)
# {
#   pacificNorthwestLip = read_feather(file.path("UnitTests/Elliott/lip", pacificNorthwestLipFile), mmap = FALSE)
#   pacificNorthwestLipFeather = arrow_table(pacificNorthwestLip, 
#                                            schema = schema(dbh = float32(), heightDiameterRatio = uint8(), crownRadius = float32(), 
#                                                            size = uint8(), centerIndex = uint8(), x = uint8(), y = uint8(), value = float32()))
#   write_feather(pacificNorthwestLipFeather, file.path("UnitTests/Elliott/lip", pacificNorthwestLipFile))
# }
#
# kalkalpenLipFiles = list.files("UnitTests/Kalkalpen/lip", pattern = "\\.feather$")
# for (kalkalpenLipFile in kalkalpenLipFiles)
# {
#   kalkalpenLip = read_feather(file.path("UnitTests/Kalkalpen/lip", kalkalpenLipFile), mmap = FALSE)
#   kalkalpenLipFeather = arrow_table(kalkalpenLip, 
#                                     schema = schema(dbh = float32(), heightDiameterRatio = uint8(), crownRadius = float32(), 
#                                                     size = uint8(), centerIndex = uint8(), x = uint8(), y = uint8(), value = float32()))
#   write_feather(kalkalpenLipFeather, file.path("UnitTests/Kalkalpen/lip", kalkalpenLipFile))
# }

# update .feather file schemas to uint32 resource unit IDs and tree IDs
# elliottUnitTestRUweatherFeather = arrow_table(elliottUnitTestRUweather,
#                                             schema = schema(id = uint32(), centerX = float32(), centerY = float32(),
#                                                             weatherID = string(),
#                                                             soilPlantAccessibleDepth = float32(), soilThetaS = float32(), soilThetaR = float32(),
#                                                             soilVanGenuchtenAlpha = float32(), soilVanGenuchtenN = float32()))
# elliott4kResourceUnitsWeatherFeather = arrow_table(elliot4kResourceUnitsWeather,
#                                                   schema = schema(id = uint32(), centerX = float32(), centerY = float32(),
#                                                                   weatherID = string(),
#                                                                   soilPlantAccessibleDepth = float32(), soilThetaS = float32(), soilThetaR = float32(),
#                                                                   soilVanGenuchtenAlpha = float32(), soilVanGenuchtenN = float32()))
# write_feather(elliottUnitTestRUweatherFeather, "UnitTests/Elliott/gis/unit test resource units 200 m weather.feather")
# write_feather(elliott4kResourceUnitsWeatherFeather, "UnitTests/Elliott/gis/resource units unbuffered 4 km weather.feather")
# 
# tSegD_H10Cr20h10A50MD7_s04110w07020_feather = arrow_table(tSegD_H10Cr20h10A50MD7_s04110w07020,
#                                                          schema = schema(standID = uint32(), treeID = uint32(), fiaCode = uint16(),
#                                                                          dbh = float32(), height = float32(), x = float32(), y = float32()))
# tSegD_H10Cr20h10A50MD7_s04110w07050_feather = arrow_table(tSegD_H10Cr20h10A50MD7_s04110w07050,
#                                                          schema = schema(standID = uint32(), treeID = uint32(), fiaCode = uint16(),
#                                                                          dbh = float32(), height = float32(), x = float32(), y = float32()))
# write_feather(tSegD_H10Cr20h10A50MD7_s04110w07020_feather, "UnitTests/Elliott/init/TSegD_H10Cr20h10A50MD7_s04110w07020.feather")
# write_feather(tSegD_H10Cr20h10A50MD7_s04110w07050_feather, "UnitTests/Elliott/init/TSegD_H10Cr20h10A50MD7_s04110w07050.feather")
