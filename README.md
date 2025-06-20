# Unity NavMesh Cleaner

Open source version of deprecated [NavMesh Cleaner](https://assetstore.unity.com/packages/tools/behavior-ai/navmesh-cleaner-151501)

## Description

NavMesh Cleaner is a script that will generate a mesh to cover unreachable NavMesh islands. Baking the NavMesh again will then mark these areas as inaccessible, rather than leaving unreachable spots in the NavMesh which helps to:

1. Reduce NavMesh size
2. Remove random movement bugs by having an incessible island specifed as the target (such as with Sample Position or on multi level structures)
3. Ensures you only have movable NavMeshes.

## Getting Started

### Dependencies

* Unity's AI Navigation Package
* Note: This is only tested with Unity 6, but in theory this should work fine in lower versions as long as you are using the AI Navigation package.

### Installing

WIP

### Executing program

WIP

## Help

WIP

## Contact

[@Acissathar](https://twitter.com/Acissathar)

Project Link: [https://github.com/Acissathar/UnityNavMeshCleaner](https://github.com/Acissathar/UnityNavMeshCleaner)

## Version History

* 1.0
    * Initial Release

## License

This project is licensed under the MIT License - see the LICENSE.md file for details

## Acknowledgments
* [VisualWorks](https://assetstore.unity.com/publishers/40160)
  - Thank you for providing permission to share the source, and for creating the original script.
