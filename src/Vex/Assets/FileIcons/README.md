# File Icons

Material Icon Theme 5.35.0 snapshot, copied from the supplied Madorin FileIcons
directory. Upstream PNG files and icon-theme.json are kept unchanged. See
SOURCE.txt and LICENSE.material-icon-theme.txt for source and MIT licensing.

Vex copies this directory to file-icons beside the application on build and
publish. macOS app bundles load it from Contents/Resources/file-icons.

Maintain Vex-specific mappings in local-overrides.json. Exact file names take
priority over extensions; compound extensions are matched longest first.
Mappings are case insensitive. Folders use named and expanded variants from the
theme. Light variants follow the current application theme. Unknown file types
and missing or corrupt assets fall back to the existing vector icons.

Add PNG files under icons and register their IDs in the iconDefinitions section
of local-overrides.json. All icon paths must remain inside this directory.
Mappings and images are cached for the application lifetime; restart after
editing them. Display size stays 12 x 12 with a 24-pixel tree row.
