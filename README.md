# NpmSymCache
Facilitates instant `npm install` actions when the package.json dependencies have not changed using a global cache and symbolic links. (Windows)
___________

```
Usage - NpmSymCache <action> -options

GlobalOption           Description
Help (-h, -?)          Shows this help
PackageFile (-p)       Where to look for the package.json file. [Default='package.json']
CacheDirectory (-d)    Overrides where NpmSymCache stores npm packages [Default='%AppData%\NpmSymCache']
CacheKey (--key)       Overrides what name NpmSymCache uses to identify this package. (usually taken from
                       package.json)
CacheLimit (--limit)   Sets number of cache entries to keep for this package [Default='5']

Actions

  Open  - Opens the root cache directory in explorer


  Clean  - Cleans the cache, only keeping the cache entries that satisfy the limit argument


  Reinstall  - Deletes current cache entry (if any) and then installs normally.


  Install  - Installs the npm packages to the cache or restores a symlink

```
