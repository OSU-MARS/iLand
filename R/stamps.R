library(dplyr)
library(ggplot2)
library(readr)
library(tidyr)
library(writexl)

theme_set(theme_bw() + theme(axis.line = element_line(size = 0.5),
                             legend.background = element_rect(fill = alpha("white", 0.5)),
                             legend.margin = margin(),
                             panel.border = element_blank()))

lipColumnTypes = cols(dbh = "d", crownRadius = "d", value = "d", .default = "i")

## reader stamps
readerStamps = read_csv("R/stamps/readerstamp.csv", col_types = lipColumnTypes) %>%
  mutate(stampID = paste(size, crownRadius))

ggplot(readerStamps %>% filter(size == 4) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c() +
  facet_wrap(vars(crownRadius))
ggplot(readerStamps %>% filter(size == 8) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c() +
  facet_wrap(vars(crownRadius))
ggplot(readerStamps %>% filter(size == 12) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c() +
  facet_wrap(vars(crownRadius))
ggplot(readerStamps %>% filter(size == 16) %>% mutate(value = na_if(value, 0))) +
  geom_raster(aes(x = x, y = y, fill = value, group = stampID)) +
  scale_fill_viridis_c() +
  facet_wrap(vars(crownRadius))

#readerStamps = bind_rows(read_csv("R/stamps/readerstamp.csv", col_types = lipColumnTypes) %>% mutate(site = "Europe"),
#                         read_csv("R/stamps/readerstamp.csv", col_types = lipColumnTypes) %>% mutate(site = "Pacific Northwest")) %>%
#  select(-dbh, -heightDiameterRatio) %>% # C++ sets DBH and height:diameter ratio to zero for reader stamps
#  pivot_wider(id_cols = c("size", "center", "crownRadius", "x", "y"), names_from = site, values_from = value)
#readerStamps %>% filter(is.na(Europe) == FALSE, is.na(`Pacific Northwest`) == FALSE, abs(Europe - `Pacific Northwest`) > 0.00001)

#readerStampAvailbility = readerStamps %>% group_by(size, center, crownRadius) %>% 
#  summarize(eu = sum(Europe, na.rm = TRUE) > 0, pnw = sum(`Pacific Northwest`, na.rm = TRUE) > 0, .groups = "drop")
#write_xlsx(readerStampAvailbility, "R/stamps/reader stamp availability.xlsx")

