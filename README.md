# MissionPlanner

![Dot Net](https://github.com/ardupilot/missionplanner/actions/workflows/main.yml/badge.svg) ![Android](https://github.com/ardupilot/missionplanner/actions/workflows/android.yml/badge.svg) ![OSX/IOS](https://github.com/ardupilot/missionplanner/actions/workflows/mac.yml/badge.svg)

Сайт: http://ardupilot.org/planner/

Форум: http://discuss.ardupilot.org/c/ground-control-software/mission-planner

Скачать последнюю стабильную версию: http://firmware.ardupilot.org/Tools/MissionPlanner/MissionPlanner-latest.msi

История изменений: https://github.com/ArduPilot/MissionPlanner/blob/master/ChangeLog.txt

Лицензия: https://github.com/ArduPilot/MissionPlanner/blob/master/COPYING.txt


## Как компилировать

### В Windows (рекомендуется)

#### 1. Установка программного обеспечения

##### Основные требования

В настоящее время Mission Planner требует:

Visual Studio 2022

##### Среда разработки

### Visual Studio Community
Для компиляции Mission Planner рекомендуется использовать Visual Studio. Visual Studio Community можно скачать со [страницы загрузки Visual Studio](https://visualstudio.microsoft.com/downloads/ "Visual Studio Download page").

Visual Studio представляет собой комплексный пакет с встроенной поддержкой Git, но из-за своей сложности он может показаться перегруженным. Чтобы упростить процесс установки, вы можете настроить его, выбрав соответствующие "Workloads" и "Individual components" в зависимости от ваших потребностей.

Чтобы упростить этот выбор, мы подготовили файл конфигурации, в котором указаны необходимые компоненты для разработки MissionPlanner. Как им воспользоваться:

1. В установщике Visual Studio откройте пункт "More".
2. Выберите "Import configuration".
3. Используйте следующий файл: [vs2022.vsconfig](https://raw.githubusercontent.com/ArduPilot/MissionPlanner/master/vs2022.vsconfig "vs2022.vsconfig").

После выполнения этих шагов будут установлены все необходимые компоненты для разработки Mission Planner.

###### VSCode
В настоящее время VSCode с плагином C# способен разобрать код, но не может его собрать.

#### 2. Получение кода

Если вы установили Visual Studio Community, вы сможете использовать Git прямо из среды IDE. Клонируйте репозиторий `https://github.com/ArduPilot/MissionPlanner.git`, чтобы получить полный исходный код.

Если IDE не установлена, Git придется установить вручную. Пожалуйста, следуйте инструкции https://ardupilot.org/dev/docs/where-to-get-the-code.html#downloading-the-code-using-git

Откройте терминал git bash в каталоге MissionPlanner и введите "git submodule update --init", чтобы загрузить все подмодули.

#### 3. Сборка

Чтобы собрать код:
- Откройте MissionPlanner.sln в Visual Studio
- В меню Build выберите "Build MissionPlanner"

### На других системах
В настоящее время сборка Mission Planner на других системах не поддерживается.

## Запуск Mission Planner на других системах

Mission Planner доступен для Android через Play Store: https://play.google.com/store/apps/details?id=com.michaeloborne.MissionPlanner
Mission Planner может работать с Mono в Linux. Учтите, что не все функции доступны на Linux.
Нативная поддержка MacOS и iOS экспериментальная и не рекомендуется неопытным пользователям: https://github.com/ArduPilot/MissionPlanner/releases/tag/osxlatest
Пользователям MacOS рекомендуется запускать Mission Planner для Windows через Boot Camp или Parallels (или аналогичные решения).

### На Linux

#### Требования

Эти инструкции проверены на Ubuntu 20.04.
Установите Mono одной из команд:
- `sudo apt install mono-complete mono-runtime libmono-system-windows-forms4.0-cil libmono-system-core4.0-cil libmono-winforms4.0-cil libmono-corlib4.0-cil libmono-system-management4.0-cil libmono-system-xml-linq4.0-cil`

#### Запуск

- Получите последнюю архивную версию Mission Planner здесь: https://firmware.ardupilot.org/Tools/MissionPlanner/MissionPlanner-latest.zip
- Распакуйте архив в нужный каталог
- Перейдите в этот каталог
- Запустите `mono MissionPlanner.exe`

Отладить Mission Planner на Mono можно с помощью `MONO_LOG_LEVEL=debug mono MissionPlanner.exe`

### Используемые внешние сервисы

| Источник | Назначение | Как отключить | Ответственный |
|---|---|---|---|
| https://firmware.oborne.me | глобальный CDN для проверки обновлений MP (проверяется раз в сутки при запуске) | изменить missionplanner.exe.config | Michael Oborne |
| https://firmware.ardupilot.org | обновления stable, метаданные прошивок, сами прошивки, уведомления, gstreamer, SRTM, SITL | обновления stable (изменить missionplanner.exe.config) - остальное невозможно | Ardupilot Team |
| https://github.com/ | обновления beta | изменить missionplanner.exe.config | Michael Oborne |
| https://raw.githubusercontent.com | старые метаданные параметров, конфигурационные файлы SITL | невозможно | Ardupilot Team |
| https://api.github.com/ | предварительная загрузка параметров ardupilot | невозможно | Ardupilot Team |
| https://raw.oborne.me/ | локальный CDN для генератора метаданных параметров, больше не основной источник | используется только по запросу пользователя, изменить missionplanner.exe.config | Michael Oborne |
| https://maps.google.com | API высот (удалено из-за злоупотребления) | N/A | N/A |
| https://discuss.cubepilot.org/ | отчеты SB2 - только на соответствующих платах при вводе данных пользователем | используется только по запросу пользователя | CubePilot |
| https://altitudeangel.com | данные UTM - включаются пользователем | используется только по запросу пользователя | Altitude Angel |
| https://autotest.ardupilot.org | метаданные журналов dataflash, метаданные параметров | невозможно | Ardupilot Team |
| Many | выбор картографического провайдера google/bing/openstreetmap/etc | выбирает пользователь | Пользователь/разные |
| https://www.cloudflare.com | геолокация для выбора NFZ | невозможно | Michael Oborne |
| https://esua.cad.gov.hk | зоны запрета полетов HK - включается пользователем | пользователь выбирает | HK Gov |
| https://ssl.google-analytics.com | Google Analytics Анонимная статистика - загрузки экранов, исключения/сбои, события (подключение), время запуска, загрузка прошивки (тип и плата) | отключается в Config > Planner > OptOut Anon Stats | Michael Oborne |
| https://api.dronelogbook.com | ведение логов - отключено | N/A | N/A |
| https://ardupilot.org | ссылки на справку на многих страницах | вызывается пользователем | ArduPilot Team |
| https://www.youtube.com | обучающие видео на многих страницах | вызывается пользователем | ArduPilot Team |
| https://files.rfdesign.com.au | прошивки RFD | вызывается пользователем | RFDesign |
| https://teck.airmarket.io | airmarket - отключено | N/A | N/A |

### Офлайн-использование - без интернета

| Расположение | Назначение | Можно перенести между ПК |
|---|---|---|
| C:\ProgramData\Mission Planner\gmapcache | Кэш карт | да |
| C:\ProgramData\Mission Planner\srtm | Кэш данных высот | да |
| C:\ProgramData\Mission Planner\\*.pdef.xml | Кэш параметров | да |
| C:\ProgramData\Mission Planner\LogMessages*.xml | Кэш метаданных DF логов | да |

В Linux это каталог /home/<user>/.local/share/Mission Planner/

### Поддерживаемые офлайн-данные
#### Высота
* Кэш SRTM
* GeoTiff в WGS84/EGM96
* DTED

#### Изображения
* Кэш карт
* WMS
* WMTS
* GDAL

### Используемые пути по умолчанию

| Расположение | Назначение |
|---|---|
| C:\ProgramData\Mission Planner | Общие данные для всех пользователей |
| C:\Users\USERNAME\Documents\Mission Planner | Данные конкретного пользователя |

В Linux это каталог /home/<user>/.local/share/Mission Planner/

### CA Cert
Сертификат центра сертификации устанавливается в корневое хранилище и используется для подписи драйверов последовательного порта Windows. Он устанавливается в рамках MSI.

[![FlagCounter](https://s01.flagcounter.com/count2/A4bA/bg_FFFFFF/txt_000000/border_CCCCCC/columns_8/maxflags_40/viewers_0/labels_1/pageviews_0/flags_0/percent_0/)](https://info.flagcounter.com/A4bA)
