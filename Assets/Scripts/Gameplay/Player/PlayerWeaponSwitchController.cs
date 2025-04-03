namespace Gameplay
{
    public class PlayerWeaponSwitchController
    {
        private PlayerInput _playerInput;
        private PlayerWeaponRepository playerWeaponRepository;

        public PlayerWeaponSwitchController(PlayerInput playerInput, PlayerWeaponRepository playerWeaponRepository)
        {
            _playerInput = playerInput;
            this.playerWeaponRepository = playerWeaponRepository;

            _playerInput.OnNextWeapon += this.playerWeaponRepository.SetNexWeapon;
            _playerInput.OnPreviousWepon += this.playerWeaponRepository.PreviousWepon;
        }
    }
}