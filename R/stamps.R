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
lightStampCsvColumnTypes = cols(dbh = "d", crownRadius = "d", value = "d", .default = "i")
readerStamps = read_csv("R/stamps/readerstamp.csv", col_types = lightStampCsvColumnTypes)
readerArrow = arrow_table(readerStamps, schema = lightStampArrowSchema)
write_feather(readerArrow, "R/stamps/readerstamp.feather", mmap = FALSE)

for (stampDirectory in c("Kalkalpen", "Pacific Northwest"))
{
  for (speciesFile in list.files(file.path("R/stamps", stampDirectory), "*.csv"))
  {
    speciesStamps = read_csv(file.path("R/stamps", stampDirectory, speciesFile), col_types = lightStampCsvColumnTypes)
    speciesArrow = arrow_table(speciesStamps, schema = lightStampArrowSchema)
    write_feather(speciesArrow, file.path("R/stamps", stampDirectory, paste0("uncomp", file_path_sans_ext(speciesFile), ".feather")))
  }
}

## expand compressed .feather in R/stamps to uncompressed feather in unit test projects
# Workaround for lack of compression support in Arrow 9.0.0.
readerStamps = read_feather("R/stamps/readerstamp.feather", mmap = FALSE)
readerArrow = arrow_table(readerStamps, schema = lightStampArrowSchema)
write_feather(readerArrow, "iLand/readerstamp.feather", compression = "uncompressed")

for (stampDirectory in c("Kalkalpen", "Pacific Northwest"))
{
  for (speciesFile in list.files(file.path("R/stamps", stampDirectory), "*.feather"))
  {
    speciesStamps = read_feather(file.path("R/stamps", stampDirectory, speciesFile))
    speciesArrow = arrow_table(speciesStamps, schema = lightStampArrowSchema)
    
    projectDirectory = stampDirectory
    if (stampDirectory == "Pacific Northwest")
    {
      projectDirectory = "Elliott"
    }
    write_feather(speciesArrow, file.path("UnitTests", projectDirectory, "lip", paste0(file_path_sans_ext(speciesFile), ".feather")), compression = "uncompressed")
  }
}


## species stamp review: Pacific Northwest species plus Kalkalpen version of Douglas-fir
# Nonzero values in stamps often form a square or nearly so, rather than being round, and most stamp intensity is on the
# upper (+y; northern) side of the tree.
#
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
#   species  DBH, cm  height:diameter ratio  stamp sizes
#   abam     4.5-150  40-150 increment 10     4-48
#   abgr     4.5-150  40-150 increment 10     4-48
#   abpr     4.5-198  40-120 increment 10     4-64
#   acma     4.5-150  30-180 increment 10    12-48
#   alru     4.5-98   40-180 increment 10     8-48
#   pipo     4.5-198  30-130 increment 10     4-64
#   pisi     4.5-198  40-150 increment 10     4-64
#   psme     4.5-222  30-160 increment 10     4-64  (4.5-150, 40-190 increment 10 for Kalkalpen in iLand 1.0)
#   thpl     4.5-198  30-190 increment 10     4-64
#   tshe     4.5-198  40-150 increment 10     4-64
#   tsme     4.5-150  40-130 increment 10     4-48
speciesStamps = bind_rows(read_feather("R/stamps/Kalkalpen/abal.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "abal"),
                          read_feather("R/stamps/Kalkalpen/acca.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "acca"),
                          read_feather("R/stamps/Kalkalpen/acpl.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "acpl"),
                          read_feather("R/stamps/Kalkalpen/acps.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "acps"),
                          read_feather("R/stamps/Kalkalpen/alal.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "alal"),
                          read_feather("R/stamps/Kalkalpen/algl.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "algl"),
                          read_feather("R/stamps/Kalkalpen/alin.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "alin"),
                          read_feather("R/stamps/Kalkalpen/bepe.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "bepe"),
                          read_feather("R/stamps/Kalkalpen/cabe.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "cabe"),
                          read_feather("R/stamps/Kalkalpen/casa.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "casa"),
                          read_feather("R/stamps/Kalkalpen/coav.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "coav"),
                          read_feather("R/stamps/Kalkalpen/fasy.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "fasy"),
                          read_feather("R/stamps/Kalkalpen/frex.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "frex"),
                          read_feather("R/stamps/Kalkalpen/lade.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "lade"),
                          read_feather("R/stamps/Kalkalpen/piab.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "piab"),
                          read_feather("R/stamps/Kalkalpen/pice.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "pice"),
                          read_feather("R/stamps/Kalkalpen/pini.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "pini"),
                          read_feather("R/stamps/Kalkalpen/pisy.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "pisy"),
                          read_feather("R/stamps/Kalkalpen/poni.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "poni"),
                          read_feather("R/stamps/Kalkalpen/potr.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "potr"),
                          read_feather("R/stamps/Kalkalpen/psme.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "psme"),
                          read_feather("R/stamps/Kalkalpen/qupe.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "qupe"),
                          read_feather("R/stamps/Kalkalpen/qupu.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "qupu"),
                          read_feather("R/stamps/Kalkalpen/quro.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "quro"),
                          read_feather("R/stamps/Kalkalpen/rops.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "rops"),
                          read_feather("R/stamps/Kalkalpen/saca.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "saca"),
                          read_feather("R/stamps/Kalkalpen/soar.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "soar"),
                          read_feather("R/stamps/Kalkalpen/soau.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "soau"),
                          read_feather("R/stamps/Kalkalpen/tico.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "tico"),
                          read_feather("R/stamps/Kalkalpen/tipl.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "tipl"),
                          read_feather("R/stamps/Kalkalpen/ulgl.feather", mmap = FALSE) %>% mutate(site = "Kalkalpen", species = "ulgl"),
                          read_feather("R/stamps/Pacific Northwest/abam.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "abam"),
                          read_feather("R/stamps/Pacific Northwest/abgr.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "abgr"),
                          read_feather("R/stamps/Pacific Northwest/abpr.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "abpr"),
                          read_feather("R/stamps/Pacific Northwest/acma.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "acma"),
                          read_feather("R/stamps/Pacific Northwest/alru.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "alru"),
                          read_feather("R/stamps/Pacific Northwest/pipo.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "pipo"),
                          read_feather("R/stamps/Pacific Northwest/pisi.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "pisi"),
                          read_feather("R/stamps/Pacific Northwest/psme.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "psme"),
                          read_feather("R/stamps/Pacific Northwest/thpl.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "thpl"),
                          read_feather("R/stamps/Pacific Northwest/tshe.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "tshe"),
                          read_feather("R/stamps/Pacific Northwest/tsme.feather", mmap = FALSE) %>% mutate(site = "Pacific Northwest", species = "tsme")) %>%
  arrange(species, site, dbh, heightDiameterRatio) %>%
  mutate(stampID = paste(site, species, dbh, heightDiameterRatio),
         stampSizeID = paste(dbh, heightDiameterRatio))

print(speciesStamps %>% group_by(site, species) %>% summarize(dbhClasses = length(unique(dbh)), hdClasses = length(unique(heightDiameterRatio)), dbhMin = min(dbh), dbhMax = max(dbh), hdRatioMin = min(heightDiameterRatio), hdRatioMax = max(heightDiameterRatio), sizeMin = min(size), sizeMax = max(size), stamps = length(unique(stampID)), normalizedStamps = stamps / (0.1 * (hdRatioMax - hdRatioMin) * 0.24 * (dbhMax - dbhMin))), n = 45)

# DBH-crown radius check plots
ggplot(speciesStamps) +
  geom_line(aes(x = dbh, y = crownRadius, color = species, group = paste(species, heightDiameterRatio))) +
  labs(x = "DBH, cm", y = "crown radius, m", color = NULL)

# stamp check plots
stampSpecies = "tshe"
ggplot(speciesStamps %>% filter(species == stampSpecies, size == 4) %>% mutate(value = na_if(value, 0))) + # no acma, alru
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c(trans = "log10") +
  facet_wrap(vars(stampSizeID))
ggplot(speciesStamps %>% filter(species == stampSpecies, size == 8) %>% mutate(value = na_if(value, 0))) + # no acma
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


## reader stamps
readerStamps = read_feather("R/stamps/readerstamp.feather", mmap = FALSE) %>%
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

## SIMD 256 bit stamp size handling for ResourceUniTrees.ApplyLightIntensityPattern256()
stamp256 = tibble(stampSize = seq(1, 64),
                  stampDataSize = if_else(stampSize <= 16, 4 * ceiling(stampSize / 4), if_else(stampSize <= 24, 24, 16 * ceiling(stampSize / 16))),
                  stampSize256 = 8 * as.integer((stampSize + 3) / 8),
                  stamp128 = stampSize > stampSize256)
ggplot(stamp256) +
  geom_segment(x = 0, y = 0, xend = 64, yend = 64, color = "grey70", linetype = "longdash") +
  geom_step(aes(x = stampSize, y = stampDataSize, color = "stamp data size"), direction = "mid") +
  geom_step(aes(x = stampSize, y = stampSize, color = "stamp size"), direction = "mid") +
  geom_step(aes(x = stampSize, y = stampSize256, color = "256 bit stamping"), direction = "mid") +
  geom_step(aes(x = stampSize, y = stampSize256 + 4 * as.integer(stamp128), color = "with 128 bit step"), direction = "mid") +
  labs(x = "stamp size", y = "stamp data size or stamp SIMD size", color = NULL) +
  scale_color_discrete(breaks = c("stamp data size", "stamp size", "256 bit stamping", "with 128 bit step")) +
  scale_x_continuous(breaks = seq(0, 64, by = 8)) +
  scale_y_continuous(breaks = seq(0, 64, by = 8)) +
  theme(legend.justification = c(0, 1), legend.position = c(0.02, 1))
print(stamp256, n = 64)


## remove obsolete iLand 0.8 Douglas-fir and western hemlock height:diameter ratios
#psme = read_feather("R/stamps/Pacific Northwest/psme iLand 0.8.feather", mmap = FALSE) %>%
#  filter(heightDiameterRatio %% 10 == 0)
#psmeArrow = arrow_table(psme, schema = lightStampArrowSchema)
#write_feather(psmeArrow, "R/stamps/Pacific Northwest/psme.feather")
#
#tshe = read_feather("R/stamps/Pacific Northwest/tshe iLand 0.8.feather", mmap = FALSE) %>%
#  filter(heightDiameterRatio %% 10 == 0)
#tsheArrow = arrow_table(tshe, schema = lightStampArrowSchema)
#write_feather(tsheArrow, "R/stamps/Pacific Northwest/tshe.feather")


## DBH-crown radius regressions: models for Pacific Northwest species are all of the form crownRadius = a0 + a1 * DBH
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
