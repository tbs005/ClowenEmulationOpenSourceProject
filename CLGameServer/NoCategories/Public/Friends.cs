﻿using System;
using CLGameServer.Client;
using CLFramework;
namespace CLGameServer
{
    public partial class PlayerMgr
    {
        /////////////////////////////////////////////////////////////////////////////////
        // Friend groups
        /////////////////////////////////////////////////////////////////////////////////
        public void FriendGroup(string type)
        {
            try
            {
                //We use string type for switch statement.
                switch (type)
                {
                    //Add new group
                    case "ADD":
                        //First check if the user has friends
                        int friendcount = DB.GetRowsCount("SELECT * FROM FRIENDS WHERE owner='" + Character.Information.CharacterID + "'");
                        //If the user has no friends return
                        if (friendcount == 0) return;
                        //Create our packet reader
                        PacketReader reader = new PacketReader(PacketInformation.buffer);
                        //Start reading information
                        string groupname = reader.Text();
                        //Close the reader
                        reader.Close();
                        //Update database information
                        DB.query("INSERT INTO friends_groups (playerid,groupname) VALUES ('" + Character.Information.CharacterID + "','" + groupname + "')");
                        //Get group id from count
                        short groupid = GetGroupId(Character.Information.CharacterID);
                        //Send packet to client
                        client.Send(Packet.FriendGroupManage("ADD", groupname, groupid, 0));
                        break;
                    //Remove group
                    case "REMOVE":
                        //Create our packet reader
                        reader = new PacketReader(PacketInformation.buffer);
                        //Start reading information
                        groupid = reader.Int16();
                        //Close the reader
                        reader.Close();
                        //Get group name
                        string groupnameinfo = GetGroupName(Character.Information.CharacterID, groupid);
                        //Update database information
                        DB.query("DELETE FROM friends_groups WHERE groupname='" + groupnameinfo + "'");
                        //Send packet to client
                        client.Send(Packet.FriendGroupManage("REMOVE", groupnameinfo, groupid, 0));
                        break;
                    //Move to group
                    case "MOVE":
                        //Create our packet reader
                        reader = new PacketReader(PacketInformation.buffer);
                        //Start reading information
                        int targetid = reader.Int32();
                        groupid = reader.Int16();
                        //Close the reader
                        reader.Close();
                        //Get groupname
                        groupnameinfo = GetGroupName(Character.Information.CharacterID, groupid);
                        //Update database information
                        DB.query("UPDATE friends SET group_name='" + groupnameinfo + "' WHERE owner='" + Character.Information.CharacterID + "' AND friend_name='" + targetid + "'");
                        //Send packet to client
                        client.Send(Packet.FriendGroupManage("MOVE", groupnameinfo, groupid, targetid));
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
        /////////////////////////////////////////////////////////////////////////////////
        // Friend groups check id
        /////////////////////////////////////////////////////////////////////////////////
        short GetGroupId(int playerid)
        {
            try
            {
                //Get count of groups
                int count = DB.GetRowsCount("SELECT * FROM friends_groups WHERE playerid='" + playerid + "'");
                //If no groups have been created
                if (count == 0) return (short)0;
                //If groups have been created
                else if (count > 0)
                {
                    //We add one + to the count since def non classified is a group
                    count = count + 1;
                    //Return the new count value
                    return (short)count;
                }
                //Def return 0
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
            return 0;
        }
        /////////////////////////////////////////////////////////////////////////////////
        // Friend groups get groupname
        /////////////////////////////////////////////////////////////////////////////////
        string GetGroupName(int playerid, short groupid)
        {
            try
            {
                //If putting back to non classified
                if (groupid == 0) return "none";
                //Get count of groups
                int count = DB.GetRowsCount("SELECT * FROM friends_groups WHERE playerid='" + playerid + "'");
                //Do for each count
                for (int i = 0; i <= count + 1; i++)
                {
                    //If the group id equals the i return the name value
                    if (i == groupid)
                    {
                        //Get groupname from db
                        string groupname = DB.GetData("SELECT groupname FROM friends_groups WHERE playerid='" + playerid + "'", "groupname");
                        //Return the groupname string value
                        return groupname;
                    }
                }
                //Def return none
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
            return "none";
        }
        /////////////////////////////////////////////////////////////////////////////////
        // Remove Friends
        /////////////////////////////////////////////////////////////////////////////////
        public void FriendRemoval()
        {
            try
            {
                //Read client packet data
                PacketReader reader = new PacketReader(PacketInformation.buffer);
                int target = reader.Int32();
                reader.Close();
                //Get player information
                PlayerMgr sys = Helpers.GetInformation.GetPlayerid(target);

                //Remove friend from our list query
                DB.query("DELETE FROM friends WHERE owner='" + Character.Information.CharacterID + "' AND friend_name='" + target + "'");
                DB.query("DELETE FROM friends WHERE owner='" + target + "' AND friend_name='" + Character.Information.CharacterID + "'");

                //Remove friend from our list packet
                client.Send(Packet.FriendData(target,3,"",Character,false));
                client.Send(Packet.FriendRemovalTarget(target));
                //Remove friend from friend id packet
                if (sys != null)
                {
                    sys.Send(Packet.FriendData(sys.Character.Information.UniqueID, 3, "", Character, false));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Friend removal error {0}",ex);
                Log.Exception(ex);
            }
        }
        /////////////////////////////////////////////////////////////////////////////////
        // Add new friend
        /////////////////////////////////////////////////////////////////////////////////
        public void FriendAdd()
        {
            try
            {
                PacketReader reader = new PacketReader(PacketInformation.buffer);
                short namelenght = reader.Int16();
                string name = reader.String(namelenght);
                reader.Close();
                if (name == Character.Information.Name) return;
                PlayerMgr sys = Helpers.GetInformation.GetPlayerName(name);
                if (sys != null)
                {
                    sys.client.Send(Packet.FriendInviteTarget(sys.Character));
                }
                else
                {
                    //Packet cannot find user
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Friend Add error {0}", ex);
                Log.Exception(ex);
            }
        }
        /////////////////////////////////////////////////////////////////////////////////
        // Friend add response
        /////////////////////////////////////////////////////////////////////////////////
        public void FriendAddResponse()
        {
            try
            {
                PacketReader reader = new PacketReader(PacketInformation.buffer);
                int Inviterid = reader.Int32();
                int Myid = reader.Int32();
                byte State = reader.Byte();
                reader.Close();

                PlayerMgr sys = Helpers.GetInformation.GetPlayer(Inviterid);
                if (sys != null)
                {
                    if (State == 0)
                    {
                        //Declined
                        client.Send(Packet.FriendDecline(Character.Information.Name));
                        sys.client.Send(Packet.FriendDecline(Character.Information.Name));
                    }
                    else
                    {
                        //Accepted
                        sys.client.Send(Packet.FriendData(Myid, 2, Character.Information.Name, Character, false));
                        client.Send(Packet.FriendData(Inviterid, 2, sys.Character.Information.Name, sys.Character, false));
                        DB.query("INSERT INTO friends (owner,friend_name,model_info) VALUES ('" + Character.Information.CharacterID + "','" + sys.Character.Information.CharacterID + "','" + sys.Character.Information.Model + "')");
                        DB.query("INSERT INTO friends (owner,friend_name,model_info) VALUES ('" + sys.Character.Information.CharacterID + "','" + Character.Information.CharacterID + "','" + Character.Information.Model + "')");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Friend Add Response Error {0}", ex);
                Log.Exception(ex);
            }
        }
    }
}
