# NavMesh Cleaner

Open source version of deprecated [NavMesh Cleaner](https://assetstore.unity.com/packages/tools/behavior-ai/navmesh-cleaner-151501)

## Description

NavMesh Cleaner is a script that will generate a mesh to cover unreachable NavMesh islands for Unity. Baking the NavMesh again will then mark these areas as inaccessible, rather than leaving unreachable spots in the NavMesh which helps to:

1. Reduce NavMesh size
2. Remove random movement bugs by having an incessible island specifed as the target (such as with Sample Position or on multi level structures)
3. Ensures you only have movable NavMeshes.

![ezgif com-optimize](https://github.com/user-attachments/assets/9d945c76-bd0e-4efe-8e83-ab6bf4e73f08)

## Getting Started

### Dependencies

* Unity's AI Navigation Package
* Note: This is only tested with Unity 6, but in theory this should work fine in lower versions as long as you are using the AI Navigation package.

### Installing

#Install via git URL

In Packaage Manager, add https://github.com/Acissathar/NavMesh-Cleaner.git as a custom gitpackage.

![image](https://github.com/user-attachments/assets/eb88d6e1-4910-487c-93e6-82f4e274dc1a)

![image](https://github.com/user-attachments/assets/d27e7c76-d30b-4007-8c9b-50ed4a82349f)

A sample scene is provided in the package as well, but must be manually imported from the Package Manager dialogue.

#Source

Download repo, and copy the NavMeshCleaner.cs file into your Unity project to modify directly.

### Executing program

#Quick Start

* Bake a NavMesh Surface using the NavMesh Surface component from Unity's AI Navigation package.
* Add a NavMesh Cleaner component to the object holding your NavMesh Surface.
* Hold Control and click all NavMesh areas you want to be considered walkable. (Symbolized with a green circle and vertical line)
* Set generated mesh settings such as height, offset, the non-walkable area type, and an override material if desired (purely visual, not necessary.)
* Click Calculate Mesh.
* Verify your islands are covered with this new mesh.
* Click Bake on the NavMesh Surface again.
* Click Hide or Remove Mesh.
* Verify your islands are now gone!

# Visual Walkthrough

* ![image](https://github.com/user-attachments/assets/948ff1f8-ffe5-4b89-a590-08d46ef4309b)

*![image](https://github.com/user-attachments/assets/8d839501-2551-4ecf-8df5-bc297b7e7593)

*![image](https://github.com/user-attachments/assets/cd32d7b6-b751-4984-bcfb-c0457cb3b947)

*![image](https://github.com/user-attachments/assets/022929c7-edbd-420b-b625-96ccd67eb3c4)

## Contact

[@Acissathar](https://twitter.com/Acissathar)

Project Link: [https://github.com/Acissathar/NavMesh-Cleaner](https://github.com/Acissathar/NavMesh-Cleaner)

## Version History

* 1.0
    * Initial Release

## License

This project is licensed under the MIT License - see the LICENSE.md file for details

## Acknowledgments
* [VisualWorks](https://assetstore.unity.com/publishers/40160)
  - Thank you for providing permission to share the source, and for creating the original script.
