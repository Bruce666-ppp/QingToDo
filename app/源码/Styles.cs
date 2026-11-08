using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Automation;

namespace QingTodo
{
    internal static class Ui
    {
        public static SolidColorBrush Brush(string hex) { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex); }
        public const string Ink = "#29362C", Muted = "#7B8379", Green = "#526D50", Paper = "#F7F8F5", Line = "#E3E8DE";
        public static void Install(Application app)
        {
            app.Resources = (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style TargetType='Window'><Setter Property='FontFamily' Value='Microsoft YaHei UI'/><Setter Property='FontSize' Value='13'/><Setter Property='Foreground' Value='#29362C'/><Setter Property='Background' Value='#F7F8F5'/><Setter Property='UseLayoutRounding' Value='True'/></Style>
  <Style TargetType='Button'>
    <Setter Property='Background' Value='Transparent'/><Setter Property='Foreground' Value='#526D50'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='10,7'/><Setter Property='Cursor' Value='Hand'/><Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='B' CornerRadius='7' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='#E7EDE2'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='Background' Value='#DCE5D6'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderThickness' Value='1'/><Setter TargetName='B' Property='BorderBrush' Value='#8FA38A'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key='Primary' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Background' Value='#526D50'/><Setter Property='Foreground' Value='White'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='B' Background='{TemplateBinding Background}' CornerRadius='8' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='#425D40'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='Background' Value='#344D32'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#AFC4A7'/><Setter TargetName='B' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  <Style TargetType='TextBox'><Setter Property='Background' Value='White'/><Setter Property='Foreground' Value='#29362C'/><Setter Property='CaretBrush' Value='#526D50'/><Setter Property='BorderBrush' Value='#DDE4D8'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='9,7'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='SelectionBrush' Value='#B9CBB0'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border x:Name='B' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7'><ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#8FA38A'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  <Style TargetType='CheckBox'><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Foreground' Value='#29362C'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'><StackPanel Orientation='Horizontal'><Border x:Name='Box' Width='19' Height='19' CornerRadius='5' BorderThickness='1.3' BorderBrush='#BAC7B4' Background='Transparent' VerticalAlignment='Center'><Path x:Name='Tick' Visibility='Collapsed' Data='M 4,9 L 7.5,12.5 L 14.5,5.5' Stroke='White' StrokeThickness='1.6' StrokeStartLineCap='Round' StrokeEndLineCap='Round'/></Border><ContentPresenter Margin='10,0,0,0' VerticalAlignment='Center'/></StackPanel><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Box' Property='Background' Value='#607B57'/><Setter TargetName='Box' Property='BorderBrush' Value='#607B57'/><Setter TargetName='Tick' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='#526D50'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Box' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  <Style TargetType='ComboBox'><Setter Property='MinHeight' Value='32'/><Setter Property='Padding' Value='8,4'/><Setter Property='Background' Value='White'/><Setter Property='BorderBrush' Value='#DDE4D8'/></Style>
  <Style TargetType='DatePicker'><Setter Property='MinHeight' Value='34'/><Setter Property='Background' Value='White'/><Setter Property='BorderBrush' Value='#DDE4D8'/><Setter Property='Language' Value='zh-CN'/></Style>
  <Style TargetType='ScrollViewer'><Setter Property='HorizontalScrollBarVisibility' Value='Disabled'/><Setter Property='VerticalScrollBarVisibility' Value='Auto'/></Style>
  <Style TargetType='ToolTip'><Setter Property='Background' Value='#29362C'/><Setter Property='Foreground' Value='White'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='9,5'/></Style>
</ResourceDictionary>");
        }
        public static TextBlock Text(string value, double size, string color)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = Brush(color), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        }
        public static Button Button(string label, RoutedEventHandler action)
        {
            var button = new Button { Content = label };
            if (action != null) button.Click += action;
            return button;
        }
        public static Button Icon(string glyph, string label, RoutedEventHandler action)
        {
            var button = Button(glyph, action);
            button.FontFamily = new FontFamily("Segoe MDL2 Assets");
            button.FontSize = 14; button.Width = 30; button.Height = 30; button.Padding = new Thickness(0);
            button.ToolTip = label;
            AutomationProperties.SetName(button, label);
            return button;
        }
        public static Border Card(UIElement child, Thickness padding)
        {
            return new Border { Background = Brushes.White, BorderBrush = Brush(Line), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = padding, Child = child };
        }
    }
}
