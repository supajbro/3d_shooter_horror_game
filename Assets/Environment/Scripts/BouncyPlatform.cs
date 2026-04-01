using UnityEngine;

public class BouncyPlatform : MonoBehaviour
{
    [Header("Bounce Settings")]
    [SerializeField] private float bounceForce = 15f;

    private void OnTriggerEnter(Collider other)
    {
        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            ApplyBounce(cc);
        }
    }

    private void ApplyBounce(CharacterController player)
    {
        // Assuming player has a simple movement script with velocity
        StarterAssets.FirstPersonController movement = player.GetComponent<StarterAssets.FirstPersonController>();
        if (movement != null)
        {
            movement.SetVerticalVelocity(bounceForce);
        }
        else
        {
            // Fallback: move the player directly
            Vector3 up = Vector3.up * bounceForce;
            player.transform.position += up * Time.deltaTime; // very simple, may need tuning
        }
    }
}