using System.Runtime.CompilerServices;

// The editor (ids, catalogs, inspectors) and the module's tests work with the hidden registry and object state
[assembly: InternalsVisibleTo("SaveSystem.Editor")]
[assembly: InternalsVisibleTo("SaveSystem.Tests")]
[assembly: InternalsVisibleTo("SaveSystem.Tests.Runtime")]
