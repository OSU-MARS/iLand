library(arrow)
library(dplyr)
library(ggplot2)
library(readr)
library(tidyr)
library(tools)
library(writexl)

theme_set(theme_bw() + theme(axis.line = element_line(size = 0.5),
                             legend.background = element_rect(fill = alpha("white", 0.5)),
                             legend.margin = margin(),
                             panel.border = element_blank()))

lightStampCsvColumnTypes = cols(dbh = "d", crownRadius = "d", value = "d", .default = "i")
lightStampArrowSchema = schema(dbh = float32(), 
                               heightDiameterRatio = uint8(),
                               crownRadius = float32(),
                               size = uint8(),
                               centerIndex = uint8(),
                               x = uint8(),
                               y = uint8(),
                               value = float32())

## transcode longform .csv files from Export-LightStamps to longform .feather for committing to git
# compressed longform is 45% smaller than iLand C++ 1.0's uncompressed binary format and 16x smaller than .csv
# uncompressed longform is 4.2x larger than iLand C++ 1.0
# writing one record batch per stamp (using RecordBatchFileWriter) increases file sizes by ~4 over longform
readerStamps = read_csv("R/stamps/readerstamp.csv", col_types = lightStampCsvColumnTypes)
readerArrow = arrow_table(readerStamps, schema = lightStampArrowSchema)
write_feather(readerArrow, "R/stamps/readerstamp.feather")

for (stampDirectory in c("R/stamps/Kalkalpen", "R/stamps/Pacific Northwest"))
{
  for (speciesFile in list.files(stampDirectory, "*.csv"))
  {
    speciesStamps = read_csv(file.path(stampDirectory, speciesFile), col_types = lightStampCsvColumnTypes)
    speciesArrow = arrow_table(speciesStamps, schema = lightStampArrowSchema)
    write_feather(speciesArrow, file.path(stampDirectory, paste0("uncomp", file_path_sans_ext(speciesFile), ".feather")))
  }
}

## species stamp review: Pacific Northwest species plus Kalkalpen version of Douglas-fir
# DBH classes: stamp DBHes are set to middle of diameter class
#   size, cm  width, cm  center DBH, cm    size, cm   width, cm  center DBH, cm
#   <5        5          4.5                32-36     4          34
#    5-6      1          5.5                36-40     4          38
#    6-7      1          6.5                40-44     4          42
#    7-8      1          7.5                44-48     4          46
#    8-9      1          8.5                48-52     4          50
#    9-10     1          9.5                52-56     4          54
#    10-12    2          11                 56-60     4          58
#    12-14    2          13                 60-64     4          62
#    14-16    2          15                 64-68     4          66
#    16-18    2          17                 68-72     4          70
#    18-20    2          19                 72-76     4          74
#    20-24    4          22                 76-80     4          78
#    24-28    4          26                 ...
#    28-32    4          30                220-224    4          222
# height:diameter classes: stamp ratios are set to middle of ratio class
#   ratio     center    ratio     center
#   <45       40        115-125   120
#    45-55    50        125-135   130
#    55-65    60        135-145   140
#    65-75    70        145-155   150
#    75-85    80        155-165   160
#    85-95    90        165-175   170
#    95-105   100       175-185   180
#   105-115   110       185-195   190
# iLand v0.6 stamps
#   species  DBH, cm  height:diameter ratio
#   abgr     4.5-150  40-150 increment 10
#   abpr     4.5-198  40-120 increment 10
#   acma     4.5-150  30-180 increment 10    many crown sizes are square rather than round
#   alru     4.5-98   40-180 increment 10
#   pipo     4.5-198  30-130 increment 10
#   pisi     4.5-198  40-150 increment 10
#   psme     4.5-222  30-160 increment 5     (4.5-150, 40-190 increment 10 for Kalkalpen in iLand 1.0)
#   thpl     4.5-198  30-190 increment 10
#   tshe     4.5-198  40-150 increment 5
#   tsme     4.5-150  40-130 increment 10
speciesStamps = bind_rows(read_csv("R/stamps/Pacific Northwest/abam.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "acma"),
                          read_csv("R/stamps/Pacific Northwest/abgr.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "abgr"),
                          read_csv("R/stamps/Pacific Northwest/abpr.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "abpr"),
                          read_csv("R/stamps/Pacific Northwest/acma.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "acma"),
                          read_csv("R/stamps/Pacific Northwest/alru.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "alru"),
                          read_csv("R/stamps/Pacific Northwest/pipo.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "pipo"),
                          read_csv("R/stamps/Pacific Northwest/pisi.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "pisi"),
                          read_csv("R/stamps/Pacific Northwest/psme.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "psme"),
                          read_csv("R/stamps/Kalkalpen/psme.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Kalkalpen", species = "psme"),
                          read_csv("R/stamps/Pacific Northwest/thpl.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "thpl"),
                          read_csv("R/stamps/Pacific Northwest/tshe.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "tshe"),
                          read_csv("R/stamps/Pacific Northwest/tsme.csv", col_types = lightStampCsvColumnTypes) %>% mutate(site = "Pacific Northwest", species = "tsme")) %>%
  arrange(species, site, dbh, heightDiameterRatio) %>%
  mutate(stampID = paste(site, species, dbh, heightDiameterRatio),
         stampSizeID = paste(dbh, heightDiameterRatio))

speciesStamps %>% group_by(site, species) %>% summarize(dbhMin = min(dbh), dbhMax = max(dbh), hdRatioMin = min(heightDiameterRatio), hdRatioMax = max(heightDiameterRatio), sizeMin = min(size), sizeMax = max(size), stamps = length(unique(stampID)), normalizedStamps = stamps / (0.1 * (hdRatioMax - hdRatioMin) * 0.24 * (dbhMax - dbhMin)))

# DBH-crown radius check plots
ggplot(speciesStamps %>% filter(species != "acma")) +
  geom_line(aes(x = dbh, y = crownRadius, color = species, group = paste(species, heightDiameterRatio))) +
  labs(x = "DBH, cm", y = "crown radius, m", color = NULL)
ggplot(speciesStamps %>% filter(species == "acma")) +
  geom_abline(intercept = 0.93, slope = 0.035, color = "grey70", linetype = "longdash") +
  geom_abline(intercept = 1.22, slope = 0.058, color = "grey70", linetype = "longdash") +
  geom_line(aes(x = dbh, y = crownRadius, color = species, group = paste(species, heightDiameterRatio))) +
  labs(x = "DBH, cm", y = "crown radius, m", color = NULL)

# stamp check plots
stampSpecies = "acma"
ggplot(speciesStamps %>% filter(species == stampSpecies, size == 4) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(stampSizeID))
ggplot(speciesStamps %>% filter(species == stampSpecies, size == 8) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(stampSizeID))
ggplot(speciesStamps %>% filter(species == stampSpecies, size == 12) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(stampSizeID))
ggplot(speciesStamps %>% filter(species == stampSpecies, size == 16) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(stampSizeID))
ggplot(speciesStamps %>% filter(species == stampSpecies, size == 24) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(stampSizeID))
ggplot(speciesStamps %>% filter(species == stampSpecies, size == 32) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(stampSizeID))
ggplot(speciesStamps %>% filter(species == stampSpecies, size == 48) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(crownRadius))
ggplot(speciesStamps %>% filter(species == stampSpecies, size == 64) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(stampSizeID))

ggplot(speciesStamps %>% filter(species == stampSpecies, dbh == 9.5) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c() +
  facet_wrap(vars(crownRadius))

## reader stamps
readerStamps = read_csv("R/stamps/readerstamp.csv", col_types = lightStampCsvColumnTypes) %>%
  mutate(stampID = paste(size, crownRadius))

ggplot(readerStamps %>% filter(size == 4) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(crownRadius))
ggplot(readerStamps %>% filter(size == 8) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(crownRadius))
ggplot(readerStamps %>% filter(size == 12) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(crownRadius))
ggplot(readerStamps %>% filter(size == 16) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(crownRadius))

# DBH-crown radius regressions: models for Pacific Northwest species are all of the form crownRadius = a0 + a1 * DBH
# species  a0	           a1
# abpr  	 1.03328	     0.040476
# acma    (0.93, 1.22)  (0.035, 0.058) - iLand 0.6's acma.bin has stamps from two slopes
# alru	   1.43190       0.065170
# pipo	   1.17990	     0.047943
# pisi	   1.02388	     0.041690
# psme	   1.16920	     0.048670
# thpl	   1.01448	     0.042182
# tshe	   1.09730	     0.045773
# tsme	   0.93632	     0.031936
#crownModelLinear = lm(crownRadius ~ dbh*species + species, speciesStamps %>% filter(species != "acma")) # R² = adj R² = 1.000, all p < 1E-15
#summary(crownModelLinear)

# Douglas-fir: Kalkalpen versus Pacific Northwest
#ggplot(speciesStamps %>% filter(species == "psme", dbh == 4.5, heightDiameterRatio == 100) %>% mutate(value = na_if(value, 0))) +
#  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
#  scale_fill_viridis_c() +
#  facet_wrap(vars(site))

#specieStampAvailability = speciesStamps %>% 
#  pivot_wider(id_cols = c("species", "dbh", "heightDiameterRatio", "size", "centerIndex", "crownRadius", "x", "y"), names_from = site, values_from = value) %>%
#  group_by(species, dbh, heightDiameterRatio, size, centerIndex, crownRadius) %>% 
#  summarize(eu = sum(Kalkalpen, na.rm = TRUE) > 0, 
#            pnw = sum(`Pacific Northwest`, na.rm = TRUE) > 0, 
#            sumAbsoluteDifference = sum(abs(Kalkalpen - `Pacific Northwest`), na.rm = TRUE),
#            .groups = "drop")
#write_xlsx(specieStampAvailability, "R/stamps/species stamp availablity.xlsx")
