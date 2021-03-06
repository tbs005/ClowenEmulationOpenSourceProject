﻿using System;
using System.Data.SqlClient;
using CLGameServer.Client;
using CLFramework;
namespace CLGameServer
{
    public partial class PlayerMgr
    {
        ///////////////////////////////////////////////////////////////////////////
        // Move item to pet
        ///////////////////////////////////////////////////////////////////////////
        void MoveItemToPet(int itemid, byte f_slot, byte t_slot)
        {
            try
            {
                ObjData.slotItem item = GetItem((uint)Character.Information.CharacterID, f_slot, 0);
                DB.query("UPDATE char_items SET slot='"+ t_slot +"',storagetype='2',pet_storage_id='" + Character.Grabpet.Grabpetid + "' WHERE owner='" + Character.Information.CharacterID + "' AND id='" + item.dbID + "'");
                client.Send(Packet.MoveItemPet(itemid, f_slot, t_slot, Character.Grabpet.Details, 0, "MOVE_TO_PET"));
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        ///////////////////////////////////////////////////////////////////////////
        // Move item from pet
        ///////////////////////////////////////////////////////////////////////////
        void MoveItemFromPet(int itemid, byte f_slot, byte t_slot)
        {
            try
            {
                ObjData.slotItem item = GetItem((uint)Character.Information.CharacterID, f_slot, 2);
                DB.query("UPDATE char_items SET itemnumber='item" + t_slot + "',slot='" + t_slot + "',storagetype='0',pet_storage_id='0' WHERE owner='" + Character.Information.CharacterID + "' AND id='" + item.dbID + "'");
                client.Send(Packet.MoveItemPet(itemid, f_slot, t_slot, Character.Grabpet.Details, 0, "MOVE_FROM_PET"));
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        ///////////////////////////////////////////////////////////////////////////
        // Move inside pet
        ///////////////////////////////////////////////////////////////////////////
        void MovePetToPet(int itemid, byte f_slot, byte t_slot, short info)
        {
            try
            {
                ObjData.slotItem itemfrom = GetItem((uint)Character.Information.CharacterID, f_slot, 2);
                ObjData.slotItem itemto = GetItem((uint)Character.Information.CharacterID, t_slot, 2);

                if (itemto.ID != 0)
                {
                    DB.query("UPDATE char_items SET itemnumber='item" + t_slot + "',slot='" + t_slot + "' WHERE id='" + itemfrom.dbID + "' AND owner='" + Character.Information.CharacterID + "' AND pet_storage_id='" + Character.Grabpet.Grabpetid + "'");
                    DB.query("UPDATE char_items SET itemnumber='item" + f_slot + "',slot='" + f_slot + "' WHERE id='" + itemto.dbID + "' AND owner='" + Character.Information.CharacterID + "' AND pet_storage_id='" + Character.Grabpet.Grabpetid + "'");
                }
                else
                {
                    DB.query("UPDATE char_items SET itemnumber='item" + t_slot + "',slot='" + t_slot + "' WHERE id='"+ itemfrom.dbID +"' AND owner='" + Character.Information.CharacterID + "' AND pet_storage_id='" + Character.Grabpet.Grabpetid + "'");
                }
                client.Send(Packet.MoveItemPet(itemid, t_slot, f_slot, Character.Grabpet.Details, info, "MOVE_INSIDE_PET"));
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        ///////////////////////////////////////////////////////////////////////////
        // Load grabpet
        ///////////////////////////////////////////////////////////////////////////
        public void HandleGrabPet(byte slot, int ItemID)
        {
            try
            {
                //Checks before we continue (Level check).
                if (!CheckItemLevel(Character.Information.Level, ItemID))
                {
                    client.Send(Packet.MoveItemError(0x6C, 0x18));
                }
                //Else we continue
                else
                {
                    //Our database query for loading pet information.
                    DB ms = new DB("SELECT * FROM pets WHERE pet_itemid='" + ItemID + "' AND playerid='" + Character.Information.CharacterID + "'");
                    //Get detailed item information.
                    ObjData.slotItem item = GetItem((uint)Character.Information.CharacterID, slot, 0);
                    //Get item model information
                    int model = ObjData.objectdata.GetItem(ObjData.Manager.ItemBase[ItemID].ObjectName);
                    //Create new pet object
                    WorldMgr.pet_obj o = new WorldMgr.pet_obj();
                    //Our sql data reader
                    using (SqlDataReader reader = ms.Read())
                    {
                        //While our reader is open we read all info below.
                        while (reader.Read())
                        {
                            int itemid                  = reader.GetInt32(7);
                            Character.Grabpet.Grabpetid = item.dbID;
                            o.UniqueID                  = Character.Grabpet.Grabpetid;
                            o.Model                     = model;
                            o.Slots                     = reader.GetByte(8);
                            o.x = Character.Position.x + Rnd.Next(1, 3);
                            o.z                         = Character.Position.z;
                            o.y = Character.Position.y + Rnd.Next(1, 3);
                            o.xSec                      = Character.Position.xSec;
                            o.ySec                      = Character.Position.ySec;
                            o.OwnerID                   = Character.Information.CharacterID;
                            o.OwnerName                 = Character.Information.Name;
                            o.Walking                   = Character.Position.Walking;
                            o.Petname                   = reader.GetString(3);
                            o.Named                     = 2;
                            o.Run                       = Character.Speed.RunSpeed;
                            o.Walk                      = Character.Speed.WalkSpeed;
                            o.Zerk                      = Character.Speed.BerserkSpeed;
                        }
                        ms.Close();
                    }
                    //We set our pet active bool, so user cannot spawn multiple.
                    Character.Grabpet.Active    = true;
                    o.Information               = true;
                    //Set all details above to definitions
                    Character.Grabpet.Details   = o;
                    //Global spawn the pet
                    Helpers.Manager.HelperObject.Add(o);
                    //Spawn ourselfs
                    o.SpawnMe();
                    //Send then packet required (Pet information block).
                    client.Send(Packet.Pet_Information_grab(o, slot));
                    //Update pet status to active (For relog purposes).
                    DB.query("UPDATE pets SET pet_active='1' WHERE pet_unique='" + Character.Grabpet.Grabpetid + "' AND playerid='" + Character.Information.CharacterID + "'");
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        ///////////////////////////////////////////////////////////////////////////
        // Rename grab pet
        ///////////////////////////////////////////////////////////////////////////
        public void RenamePet()
        {
            try
            {
                //Start reading packet data
                PacketReader Reader = new PacketReader(PacketInformation.buffer);
                //Pet id
                int petid           = Reader.Int32();
                //Pet name lenght
                short petnamel      = Reader.Int16();
                //Pet name
                string petname      = Reader.String(petnamel);
                //Check availability for pet name.
                int nameavailable   = DB.GetRowsCount("SELECT pet_name FROM pets WHERE pet_name='" + petname + "'");
                //If available (Row count is zero).
                if (nameavailable == 0)
                {
                    //Create the query we will use
                    DB ms = new DB("SELECT * FROM pets WHERE playerid='" + Character.Information.CharacterID + "' AND pet_unique='" + petid + "'");
                    //Open our data reader
                    using (SqlDataReader reader = ms.Read())
                    {
                        //While the reader is reading from database
                        while (reader.Read())
                        {
                            //First we check the lenght of the name.
                            if (petnamel < 3)
                            {
                                client.Send(Packet.Message(OperationCode.SERVER_PET_RENAME_MSG, Messages.UIIT_MSG_COSPETERR_PETNAME_NOTPUT));
                            }
                            //Check if renamed allready. (Should not be needed just extra check)
                            if (Character.Grabpet.Details != null)
                            {
                                if (petid == Character.Grabpet.Details.UniqueID)
                                {
                                    if (Character.Grabpet.Details.Petname == "No name")
                                    {
                                        //Update name in database
                                        DB.query("UPDATE pets SET pet_state='2',pet_name='" + petname + "' WHERE pet_unique='" + petid + "' AND playerid='" + Character.Information.CharacterID + "'");
                                        //Send needed packets to update name (Official sends 2 times)...
                                        client.Send(Packet.PetSpawn(petid, 2, Character.Grabpet.Details));
                                        //Send to all currently in spawn range
                                        Send(Packet.PetSpawn(petid, 2, Character.Grabpet.Details));
                                    }
                                }
                            }
                            //Check if renamed allready. (Should not be needed just extra check)
                            if (Character.Attackpet.Details != null)
                            {
                                if (petid == Character.Attackpet.Details.UniqueID)
                                {
                                    if (Character.Attackpet.Details.Petname == "No name")
                                    {
                                        //Update name in database
                                        DB.query("UPDATE pets SET pet_state='2',pet_name='" + petname + "' WHERE pet_unique='" + petid + "' AND playerid='" + Character.Information.CharacterID + "'");
                                        //Send needed packets to update name (Official sends 2 times)...
                                        client.Send(Packet.PetSpawn(petid, 2, Character.Attackpet.Details));
                                        //Send to all currently in spawn range
                                        Send(Packet.PetSpawn(petid, 2, Character.Attackpet.Details));
                                    }
                                }
                            }
                        }
                    }
                }
                //If name has been taken
                else
                {
                    //Not sure if correct msg.
                    client.Send(Packet.Message(OperationCode.SERVER_PET_RENAME_MSG, Messages.UIIT_MSG_COSPETERR_PETNAME_NOTPUT));
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        ///////////////////////////////////////////////////////////////////////////
        // Grabpet settings
        ///////////////////////////////////////////////////////////////////////////
        public void GrabPetSettings()
        {
            //Not worked on yet.
            PacketReader Reader         = new PacketReader(PacketInformation.buffer);
            int petid                   = Reader.Int32();
            byte type                   = Reader.Byte();
            int settingsinfo            = Reader.Int32();

            client.Send(Packet.ChangePetSettings(1, petid, type, settingsinfo));
        }
        ///////////////////////////////////////////////////////////////////////////
        // Despawn pets
        ///////////////////////////////////////////////////////////////////////////
        public void UnSummonPet()
        {
            try
            {
                PacketReader reader = new PacketReader(PacketInformation.buffer);
                int petid = reader.Int32();
                reader.Close();
                //First we close the pet by calling closepet void.
                if (petid == Character.Grabpet.Grabpetid)
                {
                    ClosePet(petid, Character.Grabpet.Details);
                    Character.Grabpet.Active = false;
                    Character.Grabpet.Spawned = false;
                    Character.Grabpet.Details = null;
                }
                else if (petid == Character.Attackpet.Uniqueid)
                {
                    ClosePet(petid, Character.Attackpet.Details);
                    Character.Attackpet.Active = false;
                    Character.Attackpet.Spawned = false;
                    Character.Attackpet.Details = null;
                }
                //Update database information
                DB.query("UPDATE pets SET pet_active='0' WHERE pet_unique='" + petid + "' AND playerid='" + Character.Information.CharacterID + "'");
                //Set active to false so the user can spawn another pet.
                Character.Grabpet.Active = false;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        void UnSummonPetLogoff(int petid)
        {
            try
            {
                //First we close the pet by calling closepet void.
                if (petid == Character.Grabpet.Grabpetid)
                {
                    ClosePet(petid, Character.Grabpet.Details);
                    Character.Grabpet.Active = false;
                    Character.Grabpet.Spawned = false;
                    Character.Grabpet.Details = null;
                }
                else if (petid == Character.Attackpet.Uniqueid)
                {
                    ClosePet(petid, Character.Attackpet.Details);
                    Character.Attackpet.Active = false;
                    Character.Attackpet.Spawned = false;
                    Character.Attackpet.Details = null;
                }
                //Update database information
                DB.query("UPDATE pets SET pet_active='0' WHERE pet_unique='" + petid + "' AND playerid='" + Character.Information.CharacterID + "'");
                //Set active to false so the user can spawn another pet.
                Character.Grabpet.Active = false;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
    }
}
