library(dplyr)
library(ggplot2)
library(tidyr)

theme_set(theme_bw() + theme(axis.line = element_line(linewidth = 0.5),
                             legend.background = element_rect(fill = alpha("white", 0.5)),
                             legend.margin = margin(),
                             panel.border = element_blank()))


## height-diameter ratio limits
# TreeListSpatial::GetRelativeHeightGrowth()
#heightDiameterPredictionLimits = crossing(d = seq(0, 300), species = c("psme", "alru", "tshe", "acma", "thpl")) %>% 
heightDiameterPredictionLimits = crossing(d = seq(0, 300), species = c("tshe")) %>% 
  mutate(hdRatioLow = case_when(species == "psme" ~ pmin(0.85*170.7057*1.6*d^(-0.28932*1.9),110),
                                species == "alru" ~ pmin(265.604*d^-0.43856,150),
                                species == "tshe" ~ pmin(1*87.24532*1.3*d^(-0.13491*2), 110),
                                species == "acma" ~ pmin(586.519*1.7*d^(-0.65802*1.5),150),
                                species == "thpl" ~ pmin(1*123.838*1.6*d^(-0.28606*1.9),110)),
         hdRatioHigh = case_when(species == "psme" ~ pmin(0.85*274.8253*1.25*d^(-0.32622*1.4),180),
                                 species == "alru" ~ pmin(579.8902*d^-0.53878,250),
                                 species == "tshe" ~ pmin(0.78*302.2764*1.25*d^(-0.35861*0.85*1.4), 180),
                                 species == "acma" ~ pmin(1074.09*1.6*d^(-0.74946*1.4),250),
                                 species == "thpl" ~ pmin(1*362.3185*1.35*d^(-0.43082*1.4),180)))
heightDiameterPredictionLimits %>% filter(hdRatioLow >= hdRatioHigh) %>% group_by(species) %>% summarize(dbh = min(d))

ggplot() +
  geom_segment(aes(x = 0, xend = 300, y = 10, yend = 10), color = "grey70", linetype = "longdash") +
  geom_line(aes(x = d, y = hdRatioLow, color = species), heightDiameterPredictionLimits) +
  geom_line(aes(x = d, y = hdRatioHigh, color = species), heightDiameterPredictionLimits) +
  labs(x = "DBH, cm", y = "height-diameter ratio", color = NULL)



## simplification of iLand 1.0 C++ Pinus cembra HDhigh expression
# Pinus cembra is curious for using (1 + power) where all other species have (1 - power).
# ((148.117+227.670635)/2)*0.7*(1-((-0.1649-0.317)/2))*d^((-0.1649-0.317)/2) = 0.7*187.8938*(1+0.24095)*d^-0.24095
