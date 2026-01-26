"""Boot configuration for Feather S3"""
import usb_cdc
import storage

#storage.disable_usb_drive()

usb_cdc.enable(console=False, data=True)

