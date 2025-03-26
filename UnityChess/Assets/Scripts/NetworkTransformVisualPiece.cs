using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// A NetworkTransform specialized for chess piece movement.
/// This component synchronizes the transform of visual pieces across the network.
/// </summary>
public class NetworkTransformVisualPiece : NetworkTransform
{
    // Override the base NetworkTransform settings to optimize for chess piece movement
    protected override void Awake()
    {
        base.Awake();
        
        // Configure the NetworkTransform for chess pieces
        // Only use settings available in your version of Netcode
        
        // We'll sync position but be selective about rotation components
        SyncPositionX = true;
        SyncPositionY = true;
        SyncPositionZ = true;
        SyncRotAngleX = false;  // Chess pieces typically don't rotate in X
        SyncRotAngleY = true;   // Allow Y rotation for pieces that face different directions
        SyncRotAngleZ = false;  // Chess pieces typically don't rotate in Z
        SyncScaleX = false;     // Chess pieces don't scale
        SyncScaleY = false;
        SyncScaleZ = false;
        
        // Use half-precision for transform values (if available in your version)
        if (GetType().GetProperty("UseHalfFloatPrecision") != null)
        {
            this.GetType().GetProperty("UseHalfFloatPrecision").SetValue(this, true);
        }
    }
    
    // This can be used to restrict authority to server and owner only
    protected override bool OnIsServerAuthoritative()
    {
        return true; // Server authoritative model for chess pieces
    }
}