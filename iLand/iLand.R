# supporting R code for C#
library(dplyr)
library(ggplot2)
library(patchwork)

theme_set(theme_bw() + theme(axis.line = element_line(size = 0.5),
                             legend.background = element_rect(fill = alpha("white", 0.5)),
                             legend.margin = margin(),
                             panel.border = element_blank()))

## DateTimeExtensions
# midmonth indices, non-leap year
round(0.5 * c(J = 31, F = 28, M = 31, A = 30, M = 31, J = 30, J = 31, A = 31, S = 30, O = 31, N = 30, D = 31) + cumsum(c(0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30)))

# two basic options for month classification: binary tree for month ~= 3.67 comparisons to find month
#                                             integer divide + binary tree for error correction ~= 3 comparisons to find month
# Is 0.67 comparisons lower cost than cast to float, divide, and truncate? Likely so.
dateTimeExtensions = tibble(dayOfYearIndex = seq(0, 365),
                            month = c(rep("January", 31), rep("February", 28), rep("March", 31), rep("April", 30), rep("May", 31), rep("June", 30), rep("July", 31), rep("August", 31), rep("September", 30), rep("October", 31), rep("November", 30), rep("December", 31), NA),
                            monthIndex = c(rep(0, 31), rep(1, 28), rep(2, 31), rep(3, 30), rep(4, 31), rep(5, 30), rep(6, 31), rep(7, 31), rep(8, 30), rep(9, 31), rep(10, 30), rep(11, 31), NA),
                            monthIndexFromTruncation = floor(dayOfYearIndex / 30.437),
                            leapMonthIndex = c(rep(0, 31), rep(1, 29), rep(2, 31), rep(3, 30), rep(4, 31), rep(5, 30), rep(6, 31), rep(7, 31), rep(8, 30), rep(9, 31), rep(10, 30), rep(11, 31)),
                            leapMonthIndexFromTruncation = floor(dayOfYearIndex / 30.5))

ggplot(dateTimeExtensions) +
  geom_step(aes(x = dayOfYearIndex, y = monthIndex, color = "non-leap year"), na.rm = TRUE) +
  geom_step(aes(x = dayOfYearIndex, y = leapMonthIndex, color = "leap year")) +
  geom_step(aes(x = dayOfYearIndex, y = monthIndexFromTruncation, color = "non-leap integer truncation"), na.rm = TRUE) +
  scale_y_continuous(breaks = seq(0, 12, by = 2)) +
  labs(x = "day of year index", y = "month index", color = NULL) +
  theme(legend.justification = c(0, 1), legend.position = c(0.02, 1)) +
ggplot(dateTimeExtensions) +
  geom_path(aes(x = dayOfYearIndex, y = monthIndexFromTruncation - monthIndex, color = "non-leap year integer truncation"), na.rm = TRUE) +
  geom_path(aes(x = dayOfYearIndex, y = leapMonthIndexFromTruncation - leapMonthIndex, color = "leap year integer truncation"), na.rm = TRUE) +
  labs(x = "day of year index", y = "Δ", color = NULL) +
  theme(legend.position = "none") +
plot_layout(heights = c(0.75, 0.25))

dateTimeExtensions %>% group_by(month) %>% slice_min(dayOfYearIndex, n = 1) %>% arrange(monthIndex)
dateTimeExtensions %>% filter((monthIndex != monthIndexFromTruncation) | (leapMonthIndex != leapMonthIndexFromTruncation))
