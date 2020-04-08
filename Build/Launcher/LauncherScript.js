var patch_url = 'http://pvp3.12sky2.online/launcher/';
var client_exe = 'TwelveSky2.exe';
var client_parameter = '/100/0';
//client parameter => v2.5 use /100/0 or /99/0 or /0, => v2.0 use /0
var resolution_list = '640x480,800x600,960x720,1024x576,1024x768,1152x648,1280x720,1280x800,1280x960,1366x768,1440x900,1400x1050,1440x1080,1600x900,1600x1200,1680x1050,1856x1392,1920x1080,1920x1200,1920x1440,2048x1536,2560x1440,2560x1600';

//onclick
document.body.onmouseup = function(e)
{
   //alert(e.target.id);
   var id = e.target.id;
   if( id == 'StartButton' )
        jscallcs.start();
   else if( id == 'ExitButton' )
       jscallcs.exit();
   else if( id == 'OptionButton' )
       jscallcs.display('show');
   else if( id == 'DisplayOK' )
	   jscallcs.displayok(fullscreen,resoultion_value);
}