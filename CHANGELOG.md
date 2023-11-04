# [1.9.4](https://github.com/SiskSjet/SmartSuit/compare/v1.9.3...v1.9.4) (2023-11-04)


### Bug Fixes

* fix the issue where damage was done when it should not. Also updated build script ([dd92093](https://github.com/SiskSjet/SmartSuit/commit/dd92093e064cad4785d1e4076dc76a31b5ace471))



# [1.9.3](https://github.com/SiskSjet/SmartSuit/compare/v1.9.2...v1.9.3) (2023-04-13)


### Bug Fixes

* fix CTD when used with water mod ([ab1a681](https://github.com/SiskSjet/SmartSuit/commit/ab1a681c2601359eed17f18c5d24d822ea02002d))



# [1.9.2](https://github.com/SiskSjet/SmartSuit/compare/v1.9.1...v1.9.2) (2020-10-11)


### Bug Fixes

* fix an issue where align to gravity would not properly work when auto helmet option was not enabled ([4ac80fb](https://github.com/SiskSjet/SmartSuit/commit/4ac80fbc004973ee89fbae21af1bcae9f40e6027))
* fix an issue where jetpack would change even when when it is disabled in options ([888c8f1](https://github.com/SiskSjet/SmartSuit/commit/888c8f1148207a479a7e08f11495dbac89355df0))
* fix issues related to water mod ([03999ac](https://github.com/SiskSjet/SmartSuit/commit/03999ac6ca66e1cc1c06fede122f90dccade804e))
* fix possible exception when played on dedicated servers ([2a2836f](https://github.com/SiskSjet/SmartSuit/commit/2a2836f6eecff26935236288db3e697665ff672d))
* Fixed an issue where hydrogen bottles fill level were not included in the additional fuel warning check ([9fb703b](https://github.com/SiskSjet/SmartSuit/commit/9fb703b4408ab68c162b69aff4216e35d31cecf6))


### Features

* add support for `Water Mod` ([db65d58](https://github.com/SiskSjet/SmartSuit/commit/db65d5883c6eef55eb00d64effde519d798a145d))



# [1.9.1](https://github.com/SiskSjet/SmartSuit/compare/v1.9.0...v1.9.1) (2019-11-09)


### Bug Fixes

* fix Smarter Suit is accessing physics from parallel threads warnings ([4b0f7fa](https://github.com/SiskSjet/SmartSuit/commit/4b0f7fab37caa9d00e09bb7342161b412bf3a99d))



# [1.9.0](https://github.com/SiskSjet/SmartSuit/compare/v1.3.6...v1.9.0) (2019-08-23)


### Bug Fixes

* fix a possible NRE when character is first time changed ([d3e3426](https://github.com/SiskSjet/SmartSuit/commit/d3e3426))
* fix an issue where this mod would not load the first time the world is loaded ([4a726e9](https://github.com/SiskSjet/SmartSuit/commit/4a726e9))
* fix an issue which wouldn't enable magboot when ground is found when leaving a ladder in multiplayer ([7e8d952](https://github.com/SiskSjet/SmartSuit/commit/7e8d952))
* fix some auto align issues and disabled it by default ([3ff4bc8](https://github.com/SiskSjet/SmartSuit/commit/3ff4bc8))
* fix wrong log file path for clients in multiplayer ([0ad8125](https://github.com/SiskSjet/SmartSuit/commit/0ad8125))


### Features

* bump character to ground when in range and start code rewrite ([e467224](https://github.com/SiskSjet/SmartSuit/commit/e467224))
* implement auto align to gravity ([1a1b2b6](https://github.com/SiskSjet/SmartSuit/commit/1a1b2b6)), closes [#4](https://github.com/SiskSjet/SmartSuit/issues/4)
* implement ladder support. ([a910bc6](https://github.com/SiskSjet/SmartSuit/commit/a910bc6)), closes [#5](https://github.com/SiskSjet/SmartSuit/issues/5)
* implement option to disable align to gravity ([742d54a](https://github.com/SiskSjet/SmartSuit/commit/742d54a))
* implement option to set the align to gravity delay ([dfd2e7d](https://github.com/SiskSjet/SmartSuit/commit/dfd2e7d))
* move a step forward after climbing to the top of a ladder ([0b23e99](https://github.com/SiskSjet/SmartSuit/commit/0b23e99))
* update localization code ([953209a](https://github.com/SiskSjet/SmartSuit/commit/953209a))



# [1.3.6](https://github.com/SiskSjet/SmartSuit/compare/v1.3.5...v1.3.6) (2019-03-10)


### Bug Fixes

* fix DelayAfterManualHelmet not synronized on client on change ([8149083](https://github.com/SiskSjet/SmartSuit/commit/8149083))



# [1.3.5](https://github.com/SiskSjet/SmartSuit/compare/v1.3.4...v1.3.5) (2019-03-09)


### Bug Fixes

* fix the missing support after respawning at a survival kit ([da5e758](https://github.com/SiskSjet/SmartSuit/commit/da5e758))



# [1.3.4](https://github.com/SiskSjet/SmartSuit/compare/v1.3.3...v1.3.4) (2019-02-25)


### Bug Fixes

* fix a crash when enter a message shorter than command prefix ([0601b65](https://github.com/SiskSjet/SmartSuit/commit/0601b65))
* fix the choppy notification warning ([c5b88e2](https://github.com/SiskSjet/SmartSuit/commit/c5b88e2))



# [1.3.3](https://github.com/SiskSjet/SmartSuit/compare/v1.3.2...v1.3.3) (2019-01-30)

This is just a maintance update. No new functions or fixes are added.

* updated mod utils



# [1.3.2](https://github.com/SiskSjet/SmartSuit/compare/v1.3.1...v1.3.2) (2019-01-23)


### Bug Fixes

* fix an NRE introduced with the last update ([db5472f](https://github.com/SiskSjet/SmartSuit/commit/db5472f))



# [1.3.1](https://github.com/SiskSjet/SmartSuit/compare/v1.3.0...v1.3.1) (2019-01-21)


### Features

* implement a delay after manual helmet toogle ([269ba00](https://github.com/SiskSjet/SmartSuit/commit/269ba00))



<a name="1.3.0"></a>
# [1.3.0](https://github.com/SiskSjet/SmartSuit/compare/v1.2.1...v1.3.0) (2018-10-24)


### Features

* add support for relative dampening on cockpit exit ([63eb2b6](https://github.com/SiskSjet/SmartSuit/commit/63eb2b6))
* enable always auto helmet by default. ([8b1dcb7](https://github.com/SiskSjet/SmartSuit/commit/8b1dcb7))



<a name="1.2.2"></a>
# [1.2.2](https://github.com/SiskSjet/SmartSuit/compare/v1.2.1...v1.2.2) (2018-09-29)


### Bug Fixes

* fix 'DisableAutoDampener' option not synchronized correctly ([58a7d60](https://github.com/SiskSjet/SmartSuit/commit/58a7d60))
* fix duplicate set option result message ([ea3621e](https://github.com/SiskSjet/SmartSuit/commit/ea3621e))
* fix missing translation for low fuel warning ([9389828](https://github.com/SiskSjet/SmartSuit/commit/9389828))
* fix possible deaths with ground detection and with high gravity ([ace1b54](https://github.com/SiskSjet/SmartSuit/commit/ace1b54))
* remove 'is helmet needed' check delay when leaving cockpits ([bc0b45b](https://github.com/SiskSjet/SmartSuit/commit/bc0b45b))


### Features

* add an `HALTED_SPEED_TOLERANCE` option ([1f4ab28](https://github.com/SiskSjet/SmartSuit/commit/1f4ab28))



<a name="1.2.1"></a>
# [1.2.1](https://github.com/SiskSjet/SmartSuit/compare/v1.1.1...v1.2.1) (2018-09-23)


### Features

* add option to disable auto dampener change ([5c0b01b](https://github.com/SiskSjet/SmartSuit/commit/5c0b01b))



<a name="1.2.0"></a>
# [1.2.0](https://github.com/SiskSjet/SmartSuit/compare/v1.1.2...v1.2.0) (2018-09-23)


### Bug Fixes

* fix multiple bug in multiplayer. ([4da4a5](https://github.com/SiskSjet/SmartSuit/commit/4da4a5))

### Features

* add an optional feature for to always check if helmet is needed ([ba1aa38](https://github.com/SiskSjet/SmartSuit/commit/ba1aa38))
* add an optional feature to enable an additional fuel warning ([fe533dc](https://github.com/SiskSjet/SmartSuit/commit/fe533dc))
* set speed only if jetpack is enabled ([17aa04e](https://github.com/SiskSjet/SmartSuit/commit/17aa04e))



<a name="1.1.2"></a>
# [1.1.2](https://github.com/SiskSjet/SmartSuit/compare/v1.1.1...v1.1.2) (2018-09-21)


### Bug Fixes

* fix a mistake that broke the functionality after last update. ([65d15af](https://github.com/SiskSjet/SmartSuit/commit/65d15af))
* fix an NRE on clients ([1af806b](https://github.com/SiskSjet/SmartSuit/commit/1af806b))



<a name="1.1.1"></a>
# [1.1.1](https://github.com/SiskSjet/SmartSuit/compare/v1.1.0...v1.1.1) (2018-09-20)


### Bug Fixes

* workaround for false oxygen stats with air tightness enabled ([b6c6bee](https://github.com/SiskSjet/SmartSuit/commit/b6c6bee))



<a name="1.1.0"></a>
# [1.1.0](https://github.com/SiskSjet/SmartSuit/compare/v1.0.2...v1.1.0) (2018-09-19)


### Bug Fixes

* fix a prossible NRE when spaning as space suit in a new world ([70bab3b](https://github.com/SiskSjet/SmartSuit/commit/70bab3b))


### Features

* add support for the 'Remove all automatic jetpack activation' mod ([f87d991](https://github.com/SiskSjet/SmartSuit/commit/f87d991))
* improved dampener handling in gravity with ground in range ([9ae97e6](https://github.com/SiskSjet/SmartSuit/commit/9ae97e6))



<a name="1.0.2"></a>
# [1.0.2](https://github.com/SiskSjet/SmartSuit/compare/v1.0.1...v1.0.2) (2018-09-19)


### Bug Fixes

* fix a possible NRE when spaning as space suit in a new world ([70bab3b](https://github.com/SiskSjet/SmartSuit/commit/70bab3b))



<a name="1.0.1"></a>
# [1.0.1](https://github.com/SiskSjet/SmartSuit/compare/v1.0.0...v1.0.1) (2018-09-19)


### Bug Fixes

* fix a possible game crash on new created worlds ([75a6155](https://github.com/SiskSjet/SmartSuit/commit/75a6155))



<a name="1.0.0"></a>
# 1.0.0 (2018-09-18)


### Features

* add initial features ([4cdf40](https://github.com/SiskSjet/SmartSuit/commit/4cdf40))



<a name=""></a>
#  (2018-09-19)


### Bug Fixes

* fix a possible game crash on new created worlds ([75a6155](https://github.com/SiskSjet/SmartSuit/commit/75a6155))
* fix a prossible NRE when spaning as space suit in a new world ([70bab3b](https://github.com/SiskSjet/SmartSuit/commit/70bab3b))


### Features

* add support for the 'Remove all automatic jetpack activation' mod ([f87d991](https://github.com/SiskSjet/SmartSuit/commit/f87d991))
* improved dampener handling in gravity with ground in range ([9ae97e6](https://github.com/SiskSjet/SmartSuit/commit/9ae97e6))



