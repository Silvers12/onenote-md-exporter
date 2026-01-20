# FAQ - OneNote Md Exporter

## Problèmes et solutions rencontrés

### Build échoue avec NETSDK1045 - SDK .NET ne prend pas en charge le ciblage .NET 10.0

**Problème**: Le build échoue avec l'erreur:
```
error NETSDK1045: le SDK .NET actuel ne prend pas en charge le ciblage .NET 10.0
```

**Cause**: Le projet est configuré pour cibler .NET 10.0 (`net10.0-windows7.0`) mais le SDK .NET 10.0 n'est pas installé sur le système.

**Solution**:
1. Installer le SDK .NET 10.0 depuis https://aka.ms/dotnet/download
2. OU modifier le fichier `OneNoteMdExporter.csproj` pour cibler une version inférieure (ex: `net9.0-windows`)

---

### Export incrémental - Premier export

**Problème**: Comment fonctionne le premier export en mode incrémental?

**Solution**: Lors du premier export avec `--incremental`, toutes les pages sont exportées et un fichier manifeste `.export-manifest.json` est créé dans le dossier d'export. Les exports suivants n'exporteront que les pages modifiées.

---

### Export incrémental - Dossier avec timestamp vs dossier stable

**Problème**: Pourquoi le dossier d'export n'a plus de timestamp en mode incrémental?

**Solution**: En mode incrémental, le dossier d'export est stable (sans timestamp) pour permettre de comparer les fichiers entre les exports. Le chemin est `Export/md/NotebookName/` au lieu de `Export/md/NotebookName-YYYYMMDD HH-mm/`.

---

### Erreur 0x8004202B lors de l'export

**Problème**: L'export échoue avec l'erreur OneNote `0x8004202B`.

**Cause**: La page est possiblement corrompue ou non synchronisée avec OneDrive/SharePoint.

**Solution**:
1. Ouvrir OneNote
2. Forcer la synchronisation du notebook (Fichier > Informations > Afficher l'état de synchronisation)
3. Attendre que la synchronisation soit complète
4. Relancer l'export

---

### Fichier manifeste corrompu ou incompatible

**Problème**: Le manifeste `.export-manifest.json` semble corrompu ou génère des erreurs.

**Solution**: Supprimer le fichier `.export-manifest.json` du dossier d'export et relancer avec `--incremental`. Un nouveau manifeste sera créé avec un export complet.

---
