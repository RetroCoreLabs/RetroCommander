# Open defects

Defects that are known and not yet fixed. A test that fails because of one of these stays red
and names it here; it is not skipped and not loosened.

## B2 - The NDFS driver declares property names its files do not have

**Tests:** `NdfsUiPanelTests.CopyFile_NdfsToNative_WritesMetadataSidecar` and
`CopyFile_ExportThenReimport_RestoresPortableNdfsMetadata` (red).

**Seen:** 2026-11-23. Whether a copied file takes a `.xat` with its properties, and which ones, is
now decided only by what the disk's driver declares (RetroIO `IExtendedAttributeSupport.GetCapabilities`,
see `Operations/XatSidecarRules.cs`). The NDFS driver declares names such as `ndfs:UserName` and
`ndfs:AccessBits`, but the properties its files carry are named `ndfs.user_name`,
`ndfs.access_bits` (RetroIO `NdfsPropertyBagKeys`). None match, so nothing is kept.

**Effect:** a file copied off an NDFS disk into a Windows folder gets no `.xat`, and its owner,
access bits and other ND properties are lost on the way back in.

**Cause:** the declared list in RetroIO `NdfsDriver.GetCapabilities` was typed by hand and never
checked against the real names. The fix is in RetroIO: declare the real names, and a rule test
that every declared name occurs and every property a file carries is declared. The other drivers
(Commodore, CP/M, Dragon, PC, BSD) declare no properties yet, so their files get no `.xat` either
until they do. Reported to the RetroIO session the same day.
