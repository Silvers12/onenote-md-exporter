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

### Application plante au démarrage - appSettings.json introuvable (SingleFile publish)

**Problème**: L'application compilée en Release (SingleFile) plante avec l'erreur:
```
System.IO.FileNotFoundException: The configuration file 'appSettings.json' was not found
```

**Cause**: Les fichiers de configuration (`appSettings.json`, `LICENSE`, fichiers de traduction) étaient intégrés dans l'exécutable SingleFile au lieu d'être copiés séparément. L'application ne pouvait pas les lire car elle cherche ces fichiers sur le disque.

**Solution**: Ajouter `<ExcludeFromSingleFile>true</ExcludeFromSingleFile>` dans le fichier `.csproj` pour chaque fichier devant rester externe:
```xml
<None Update="appSettings.json">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
</None>
```

Cela s'applique à:
- `appSettings.json`
- `LICENSE`
- `Resources/trad.*.json` (fichiers de traduction)

**Corrigé dans**: v1.6.1

---

### Fichier manifeste corrompu ou incompatible

**Problème**: Le manifeste `.export-manifest.json` semble corrompu ou génère des erreurs.

**Solution**: Supprimer le fichier `.export-manifest.json` du dossier d'export et relancer avec `--incremental`. Un nouveau manifeste sera créé avec un export complet.

---

### Export incrémental - Optimisation Phase 1 (v2.0 du manifeste)

**Problème**: La Phase 1 de l'export (construction de l'arborescence) était lente car elle chargeait les pages de toutes les sections, même si aucune modification n'avait eu lieu.

**Solution implémentée**: Le manifeste v2.0 stocke maintenant les métadonnées des sections en plus des pages. Lors d'un export incrémental:
- Seules les sections modifiées ou nouvelles déclenchent un appel à OneNote pour charger leurs pages
- Les sections inchangées utilisent les données en cache du manifeste
- Gains de performance estimés: 80-99% selon le % de sections modifiées

**Comportement de migration**:
- Les manifestes v1.0 existants sont automatiquement migrés en v2.0
- Après migration, le premier export scanne toutes les sections (comportement normal)
- Les exports suivants bénéficient de l'optimisation

**Messages dans les logs**:
- `[SKIP]` - Section inchangée, pages récupérées depuis le cache
- `[NEW]` - Nouvelle section détectée
- `[LOAD]` - Section modifiée, pages chargées depuis OneNote

**Structure du manifeste v2.0**:
```json
{
  "version": "2.0",
  "sections": {
    "section-onenote-id": {
      "title": "Section Name",
      "oneNoteId": "...",
      "lastModificationDate": "2024-01-15T10:30:00",
      "path": "Notebook/Section",
      "isSectionGroup": false
    }
  },
  "pages": {
    "page-onenote-id": {
      "title": "Page Title",
      "sectionId": "section-onenote-id",
      ...
    }
  }
}
```

---

### Export incrémental - Reprise après interruption

**Problème**: Si l'export est interrompu (Ctrl+C, crash, etc.), les pages déjà exportées étaient considérées comme "nouvelles" au relancement car le manifeste n'était sauvegardé qu'à la fin.

**Solution implémentée**: Le manifeste est maintenant sauvegardé après chaque page exportée avec succès. Cela permet:
- De reprendre l'export là où il s'était arrêté
- De ne pas ré-exporter les pages déjà traitées lors de la session interrompue
- Une progression fiable même en cas d'interruption

**Comportement**:
- Chaque page exportée est immédiatement enregistrée dans le manifeste
- Au relancement, les pages déjà exportées sont détectées comme "unchanged" et ignorées
- Légère augmentation des écritures disque (1 sauvegarde/page au lieu de 1 à la fin)

---

### Export incrémental - Boucle automatique sur erreurs

**Problème**: Lorsque des pages échouent à l'export (erreurs OneNote, pages corrompues, non synchronisées), il fallait relancer manuellement l'export pour réessayer.

**Solution implémentée**: Nouvelle option `--retry-on-errors` qui boucle automatiquement tant qu'il y a des erreurs.

**Utilisation**:
- CLI : `OneNoteMdExporter.exe --incremental --retry-on-errors`
- Interactif : Répondre `[2]` à la question "Voulez-vous activer la boucle automatique en cas d'erreurs ?"
- appSettings.json : `"RetryOnErrors": true`

**Protection contre les boucles infinies**:
- Arrêt si aucun progrès (même nombre d'erreurs qu'à l'itération précédente)
- Arrêt si nombre max d'itérations atteint (par défaut : 10, configurable via `MaxRetryIterations`)

**Messages dans les logs**:
- `*** Retry iteration X (Y errors remaining) ***` - Début d'une nouvelle itération
- `--> Stopping retry: no progress made` - Arrêt car aucun progrès
- `--> All pages exported successfully after X iterations` - Succès après plusieurs tentatives

**Prérequis**:
- Mode incrémental (`--incremental`) obligatoire pour que le retry fonctionne
- Les pages en erreur ne sont pas ajoutées au manifeste, donc elles seront réessayées

---

### Export incrémental - Pages en erreur et sections [SKIP]

**Problème**: Si une page échouait à l'export, elle n'était pas ajoutée au manifeste. Si la section parente n'avait pas changé, elle était marquée [SKIP] au prochain export et la page en erreur disparaissait complètement (non rechargée depuis OneNote).

**Solution implémentée**: Le manifeste v2.0 stocke maintenant un flag `HasExportErrors` par section.
- Quand une page échoue, sa section est marquée `HasExportErrors = true`
- Au prochain export incrémental, les sections avec `HasExportErrors = true` sont forcées en [LOAD]
- Les pages en erreur sont ainsi rechargées depuis OneNote et réessayées

**Comportement attendu**:
```
# Premier export - erreur sur une page
- [LOAD] Section A
  - Page 1 ✓
  - Page 2 ✗ (erreur)
  - Page 3 ✓
→ Section A marquée HasExportErrors=true dans le manifeste

# Deuxième export - la section est forcée en LOAD
- [LOAD] Section A  ← forcé car HasExportErrors=true
  - [SKIP] Page 1
  - [UPDATE] Page 2  ← réessayée
  - [SKIP] Page 3
```

---
